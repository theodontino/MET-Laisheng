using System.Collections.Generic;

namespace MET.City {
// Presentation data only. Quotes, availability and commands belong to the adapter.
public sealed class WildStarWindowModel {
 public string Title = "星图 · 农场", Subtitle = "", StockText = "";
 public float CapacityUsed, CapacityTotal;
 public List<WildStarNodeModel> Nodes = new List<WildStarNodeModel>();
 public List<WildStarRouteModel> Routes = new List<WildStarRouteModel>();
 public string SelectedId = "compound", SelectedTitle = "", Description = "";
 public string Costs = "", ResourceCost = "", ResourceAvailable = "";
 public string DpCost = "", DpFree = "", CpCost = "", Prerequisites = "", Constraint = "";
 public bool CanConfirm, CanRemove;
 public string RemovalDescription = "", StatusText = "", ConfirmText = "确认建造";
}
public sealed class WildStarRouteModel {
 public string From = "", To = "";
 public bool Bypass;
}
public sealed class WildStarNodeModel {
 public string Id = "", Title = "", Subtitle = "";
 // available / completed / pending / locked; selection is independent of state.
 public string State = "available", StatusText = "";
}
}
