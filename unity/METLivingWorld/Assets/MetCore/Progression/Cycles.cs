using System;
using System.Linq;
namespace MET.Core {
public static class Cycles {
    public const double MONTH_SECONDS = 1555200.0;
    public const double YEAR_SECONDS = 18662400.0;
    public static void ensure(Game g) {
        if (!((g.s["block"].Get("cycles").IsMap))) {
            g.s["block"]["cycles"] = V.Map("year", 0, "major_cycle", 0, "population_stability", V.Map(), "population_awards", V.Arr(), "history", V.Arr(), "baseline", V.Map(), "pending_summaries", V.Arr());
        }
        V c = g.s["block"]["cycles"];
        if ((c["baseline"].Count == 0)) { c["baseline"] = _snapshot(g); }
    }
    public static V _snapshot(Game g) {
        V stock = V.Map();
        foreach (var r in V.Arr("food", "material", "goods")) { stock[r] = V.From(g.inventory_total(r.Text)); }
        return V.Map("currency", g.s["currency"].Double, "population", g.s["metrics"].Get("population",0).Double, "housed", g.s["metrics"].Get("housed",0).Double, "stock", stock, "pollution", g.s["metrics"].Get("pollution",0).Double, "built", (int)g.s["totals"]["built"].Long);
    }
    public static double next_boundary_seconds(Game g) {
        ensure(g);
        double seconds = g.s.Get("beta_time",V.Map()).Get("local_elapsed_seconds",0).Double;
        double next = (((int)g.s["block"]["cycles"]["year"].Long + 1) * YEAR_SECONDS);
        double result = Math.Max(0,(next - seconds));
        V cfg = g.c.beta.Get("economy_candidates",V.Map());
        double duration = (cfg.Get("milestone_stability_months",1).Double * MONTH_SECONDS);
        foreach (var threshold in cfg.Get("milestone_housed_people",V.Arr(32, 64, 128, 256))) {
            string key = V.From((int)threshold.Long).Text;
            if ((g.s["block"]["cycles"]["population_awards"].Contains(key))) { continue; }
            if ((g.s["metrics"].Get("housed",0).Double >= threshold.Double)) {
                result = Math.Min(result,Math.Max(0,(duration - g.s["block"]["cycles"]["population_stability"].Get(key,0).Double)));
            }
        }
        return result;
    }
    public static void tick(Game g, double local_seconds, double previous_housed = -(1.0)) {
        ensure(g);
        V c = g.s["block"]["cycles"];
        double elapsed = g.s.Get("beta_time",V.Map()).Get("local_elapsed_seconds",0).Double;
        V cfg = g.c.beta.Get("economy_candidates",V.Map());
        double duration = (cfg.Get("milestone_stability_months",1).Double * MONTH_SECONDS);
        foreach (var threshold in cfg.Get("milestone_housed_people",V.Arr(32, 64, 128, 256))) {
            string key = V.From((int)threshold.Long).Text;
            if ((c["population_awards"].Contains(key))) { continue; }
            double stable = c["population_stability"].Get(key,0).Double;
            double housed = g.s["metrics"].Get("housed",0).Double;
            double started_housed = ((previous_housed < 0) ? housed : previous_housed);
            stable = ((((housed + 1e-08) >= threshold.Double) && ((started_housed + 1e-08) >= threshold.Double)) ? (stable + local_seconds) : 0.0);
            c["population_stability"][key] = V.From(stable);
            if (((stable + 1e-06) >= duration)) {
                c["population_awards"].Add(key);
                Bonus.draw(g,("population:" + key),V.Arr(),V.Map("kind", "copy"));
                g.log_event("population_milestone",V.Map("threshold", threshold),string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0}位居民稳定安居，获得一份构筑机会。",(int)threshold.Long));
            }
        }
        while (((elapsed + 1e-06) >= (((int)c["year"].Long + 1) * YEAR_SECONDS))) {
            c["year"] = V.From(((int)c["year"].Long + 1));
            V now = _snapshot(g);
            V row = V.Map("year", c["year"], "local_elapsed_seconds", ((int)c["year"].Long * YEAR_SECONDS), "before", c["baseline"].Duplicate(true), "after", now, "currency_delta", (now["currency"].Double - c["baseline"]["currency"].Double), "population_delta", (now["population"].Double - c["baseline"]["population"].Double), "major_cycle", false);
            Bonus.draw(g,("annual:" + c["year"].Text),V.Arr(),V.Map("kind", "copy"));
            int cycle_years = Math.Max(1,(int)g.c.beta.Get("time",V.Map()).Get("major_cycle_years",2).Long);
            if ((((int)c["year"].Long % cycle_years) == 0)) {
                c["major_cycle"] = V.From(((int)c["year"].Long / cycle_years));
                row["major_cycle"] = V.From(true);
                Themes.on_major_cycle(g,(int)c["major_cycle"].Long);
            }
            c["history"].Add(row);
            c["pending_summaries"].Add(row.Duplicate(true));
            c["baseline"] = now;
            if ((c["history"].Count > 100)) { c["history"].Items.RemoveAt(0); }
            g.s["settings"]["user_paused"] = V.From(true);
            g.s["beta_time"]["mode"] = V.From("normal");
            g.s["beta_time"]["pause_reason"] = V.From("annual");
            g.log_event("annual",V.Map("year", c["year"], "major_cycle", row["major_cycle"]),string.Format(System.Globalization.CultureInfo.InvariantCulture,"来生第{0}年结束：查看城市变化与年度构筑机会。",(int)c["year"].Long));
        }
    }
    public static void acknowledge(Game g) {
        ensure(g);
        g.s["block"]["cycles"]["pending_summaries"].Clear();
    }
    public static V view(Game g) {
        ensure(g);
        V result = g.s["block"]["cycles"].Duplicate(true);
        result["next_year_seconds"] = V.From(Math.Max(0,((((int)result["year"].Long + 1) * YEAR_SECONDS) - g.s["beta_time"]["local_elapsed_seconds"].Double)));
        return result;
    }
}
}
