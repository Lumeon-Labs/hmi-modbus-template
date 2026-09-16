using FluentAssertions;
using HmiTemplate.Core.Data;
using HmiTemplate.Core.Models;
using HmiTemplate.Core.Services;
using Xunit;

namespace HmiTemplate.Tests;

/// <summary>
/// AlarmEngine 單元測試
/// 驗證：觸發、Hysteresis 防抖帶、清除、AcknowledgeAlarm、Hysteresis 設錯的退回行為
/// </summary>
public class AlarmEngineTests
{
    /// <summary>建立標準測試用 AlarmEngine，含 ALM-001（HR0 ≥ 800 觸發，< 780 才清除）</summary>
    private static (AlarmEngine engine, List<AlarmRecord> triggered, List<AlarmRecord> cleared)
        CreateEngine(int hysteresis = 780)
    {
        var rules = new List<AlarmRule>
        {
            new()
            {
                Id = "ALM-001",
                Description = "高溫警報",
                Register = "HR0",
                Threshold = 800,          // 80.0°C 觸發
                Hysteresis = hysteresis   // 預設 78.0°C 以下才清除
            }
        };

        var history = new AlarmHistory();
        var engine = new AlarmEngine(rules, history);

        var triggered = new List<AlarmRecord>();
        var cleared = new List<AlarmRecord>();

        engine.AlarmTriggered += r => triggered.Add(r);
        engine.AlarmCleared += r => cleared.Add(r);

        return (engine, triggered, cleared);
    }

    // ── 建立測試用 ProcessData ──────────────────

    private static ProcessData MakeData(double tempC) => new()
    {
        Temperature = tempC,
        Pressure = 101.3,
        Speed = 1500,
        Flow = 10.0,
        Timestamp = DateTime.Now
    };

    // ══════════════════════════════════════════════
    //  Test 1：溫度超過閾值 → 觸發 ALM-001
    // ══════════════════════════════════════════════
    [Fact]
    public void Temperature_ExceedsThreshold_ShouldTriggerAlarm()
    {
        var (engine, triggered, _) = CreateEngine();

        // 溫度 80.1°C（HR0 raw = 801 > 800）
        engine.Update(MakeData(80.1));

        // 稍等事件通知（Task.Run）
        Thread.Sleep(100);

        triggered.Should().HaveCount(1);
        triggered[0].AlarmId.Should().Be("ALM-001");
        triggered[0].Status.Should().Be(AlarmStatus.Active);
        engine.ActiveAlarms.Should().HaveCount(1);
    }

    // ══════════════════════════════════════════════
    //  Test 2：觸發後溫度略降但仍 >= Threshold → 警報不清除
    //  （此測試驗證只要仍超過閾值就不清除，Hysteresis 邏輯正確）
    // ══════════════════════════════════════════════
    [Fact]
    public void Temperature_DropsButStillAboveThreshold_ShouldKeepAlarm()
    {
        var (engine, triggered, cleared) = CreateEngine();

        // 先觸發
        engine.Update(MakeData(85.0));
        Thread.Sleep(100);
        triggered.Should().HaveCount(1, "應先觸發警報");

        // 溫度微降但仍 > 80°C（raw 810 > 800 = 仍觸發狀態）
        engine.Update(MakeData(81.0));
        Thread.Sleep(100);

        cleared.Should().BeEmpty("溫度仍超過閾值，警報不應清除");
        engine.ActiveAlarms.Should().HaveCount(1);
    }

    // ══════════════════════════════════════════════
    //  Test 3：溫度低於 Hysteresis（< 780）→ 警報清除
    // ══════════════════════════════════════════════
    [Fact]
    public void Temperature_DropsBelowHysteresis_ShouldClearAlarm()
    {
        var (engine, triggered, cleared) = CreateEngine();

        // 觸發
        engine.Update(MakeData(81.0));
        Thread.Sleep(100);
        triggered.Should().HaveCount(1);

        // 溫度降到 77.9°C（raw = 779 < 780 → 清除）
        engine.Update(MakeData(77.9));
        Thread.Sleep(100);

        cleared.Should().HaveCount(1);
        cleared[0].AlarmId.Should().Be("ALM-001");
        cleared[0].Status.Should().Be(AlarmStatus.Cleared);
        cleared[0].ClearedAt.Should().NotBeNull();
        engine.ActiveAlarms.Should().BeEmpty();
    }

    // ══════════════════════════════════════════════
    //  Test 4：AcknowledgeAlarm → 狀態變 Acknowledged
    // ══════════════════════════════════════════════
    [Fact]
    public void AcknowledgeAlarm_ShouldChangeStatusToAcknowledged()
    {
        var (engine, triggered, _) = CreateEngine();

        // 觸發
        engine.Update(MakeData(82.0));
        Thread.Sleep(100);
        triggered.Should().HaveCount(1);

        // 確認
        engine.AcknowledgeAlarm("ALM-001");

        engine.ActiveAlarms.Should().HaveCount(1);
        engine.ActiveAlarms[0].Status.Should().Be(AlarmStatus.Acknowledged);
        engine.ActiveAlarms[0].AcknowledgedAt.Should().NotBeNull();
    }

    // ══════════════════════════════════════════════
    //  Test 6：降到 Threshold 以下、但仍在防抖帶內（780 ≤ raw < 800）→ 不清除
    //  這就是 Hysteresis 存在的理由：邊界附近來回不會觸發/清除抖動
    // ══════════════════════════════════════════════
    [Fact]
    public void Temperature_InsideHysteresisBand_ShouldKeepAlarm()
    {
        var (engine, triggered, cleared) = CreateEngine();

        engine.Update(MakeData(80.5));   // 805 → 觸發
        Thread.Sleep(100);
        triggered.Should().HaveCount(1);

        engine.Update(MakeData(79.0));   // 790：低於 800 但未低於 780 → 保持
        engine.Update(MakeData(79.9));   // 799：同上
        engine.Update(MakeData(78.0));   // 780：等於 Hysteresis，仍未「低於」→ 保持
        Thread.Sleep(100);

        cleared.Should().BeEmpty("在防抖帶內不應清除");
        engine.ActiveAlarms.Should().HaveCount(1);
        triggered.Should().HaveCount(1, "帶內來回也不應重複觸發");

        engine.Update(MakeData(77.9));   // 779 < 780 → 清除
        Thread.Sleep(100);
        cleared.Should().HaveCount(1);
        engine.ActiveAlarms.Should().BeEmpty();
    }

    // ══════════════════════════════════════════════
    //  Test 7：Hysteresis 設錯（≥ Threshold）→ 退回用 Threshold 清除，不會卡死
    // ══════════════════════════════════════════════
    [Fact]
    public void MisconfiguredHysteresis_ShouldFallBackToThreshold()
    {
        var (engine, triggered, cleared) = CreateEngine(hysteresis: 820);

        engine.Update(MakeData(81.0));   // 810 → 觸發
        Thread.Sleep(100);
        triggered.Should().HaveCount(1);

        engine.Update(MakeData(79.9));   // 799 < 800（Threshold）→ 清除
        Thread.Sleep(100);
        cleared.Should().HaveCount(1);
        engine.ActiveAlarms.Should().BeEmpty();
    }

    // ══════════════════════════════════════════════
    //  Test 5：溫度未超過閾值 → 不觸發警報
    // ══════════════════════════════════════════════
    [Fact]
    public void Temperature_BelowThreshold_ShouldNotTrigger()
    {
        var (engine, triggered, _) = CreateEngine();

        engine.Update(MakeData(25.0));  // 正常溫度
        engine.Update(MakeData(79.9));  // 剛好低於 80°C
        Thread.Sleep(100);

        triggered.Should().BeEmpty();
        engine.ActiveAlarms.Should().BeEmpty();
    }
}
