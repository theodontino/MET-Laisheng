using System.Collections.Generic;

namespace MET.City {
// Detached, already formatted presentation data. The view never evaluates a game rule.
public sealed class UpgradeWindowModel {
 public string Title = "", Subtitle = "", CurrentTier = "", SelectedId = "";
 public string SelectedTitle = "", SelectedSummary = "";
 public string Cost = "", MaterialConstraint = "", PowerConstraint = "", BalanceAfter = "", DisabledReason = "";
 public bool CanConfirm;
 public List<UpgradeCandidateModel> Candidates = new List<UpgradeCandidateModel>();
 public List<UpgradeMetricRow> PrimaryRows = new List<UpgradeMetricRow>();
 public List<UpgradeMetricRow> CityRows = new List<UpgradeMetricRow>();
}

public sealed class UpgradeCandidateModel {
 public string Id = "", Title = "", Tier = "", Traits = "", Slots = "", Cost = "", DisabledReason = "";
 public bool Available = true;
}

public sealed class UpgradeMetricRow {
 public string Label = "", Baseline = "", Predicted = "", Delta = "", Unit = "", Hint = "";
 public bool Warning;
}
}
