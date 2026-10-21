using System;
using System.Globalization;
namespace MET.Core {
/// <summary>Native port of core/time_model.gd. UTC records absence; only active submissions run the city.</summary>
public static class Clock {
    static bool Finite(double n)=>!double.IsNaN(n)&&!double.IsInfinity(n);
    static double Clamp(double n,double a,double b)=>Math.Min(b,Math.Max(a,n));
    public static void ensure(Game g) {
        if(!g.s.Get("beta_time").IsMap)g.s["beta_time"]=V.Map("local_elapsed_seconds",g.s.Get("local_hours",0).Double*g.c.beta["units"]["earth_seconds_per_month"].Double,"active_real_seconds",0.0,"offline_recorded_seconds",0.0,"mode","normal","pause_reason","","last_seen_utc",g.s.Get("wall_mark",0).Double,"segments_committed",0);
        if(!g.s["settings"].Has("user_paused"))g.s["settings"]["user_paused"]=false;
    }
    public static double acceleration(Game g,string mode_id="") {
        string selected=mode_id!=""?mode_id:g.s.Get("beta_time",V.Map()).Get("mode","normal").Text;
        foreach(var row in g.c.beta["time"]["modes"])if(row["id"].Text==selected)return row["acceleration"].Double;
        return 1;
    }
    public static string set_mode(Game g,string mode_id) {
        ensure(g);bool found=false;
        foreach(var row in g.c.beta["time"]["modes"])if(row["id"].Text==mode_id){found=true;break;}
        if(!found)return "未知时间档位。";
        if(g.s["dt"].Double<=0&&mode_id!="normal")return "DT 已耗尽，仍可常速经营或结算后独立重开。";
        if(g.s["beta_time"]["mode"].Text!=mode_id)g.revision++;
        g.s["beta_time"]["mode"]=mode_id;return "";
    }
    public static double resume(Game g,double utc_now) {
        ensure(g);if(!Finite(utc_now))return 0;
        double previous=Math.Max(g.s["beta_time"]["last_seen_utc"].Double,g.s.Get("wall_mark",0).Double),elapsed=Math.Max(0,utc_now-previous);
        g.s["beta_time"]["offline_recorded_seconds"]=g.s["beta_time"]["offline_recorded_seconds"].Double+elapsed;
        g.s["beta_time"]["last_seen_utc"]=Math.Max(previous,utc_now);
        g.s["wall_mark"]=Math.Max(g.s.Get("wall_mark",0).Double,utc_now);
        g.s["global_now"]=Math.Max(g.s.Get("global_now",0).Double,utc_now);
        set_mode(g,"normal");string reason=g.s["beta_time"].Get("pause_reason","").Text;
        if(Array.IndexOf(new[]{"","manual","resume","offline","background","返回城市；准备好后继续"},reason)>=0)g.s["beta_time"]["pause_reason"]="返回城市；准备好后继续";
        g.s["settings"]["user_paused"]=true;return elapsed;
    }
    public static V calendar(Game g,double seconds=-1) {
        if(seconds<0)seconds=g.s.Get("beta_time",V.Map()).Get("local_elapsed_seconds",0).Double;
        var u=g.c.beta["units"];double days=Math.Max(0,seconds)/u["earth_seconds_per_laisheng_day"].Double;
        long whole=(long)Math.Floor(days+1e-10),year=u["days_per_year"].Long,month=u["days_per_month"].Long;
        double hour=(days-Math.Floor(days))*u["civil_hours_per_day"].Double;
        return V.Map("year",whole/year+1,"month",whole%year/month+1,"day",whole%month+1,"hour",(long)Math.Floor(hour),"minute",(long)Math.Floor(hour%1*60),"years",seconds/u["earth_seconds_per_year"].Double,"months",seconds/u["earth_seconds_per_month"].Double,"days",days);
    }
    public static string duration(Game g,double seconds) {
        if(!Finite(seconds))return "持续稳定";double days=Math.Max(0,seconds)/g.c.beta["units"]["earth_seconds_per_laisheng_day"].Double;
        return days>=144?(days/144).ToString("F2",CultureInfo.InvariantCulture)+"来生年":days>=12?(days/12).ToString("F2",CultureInfo.InvariantCulture)+"来生月":days>=1?days.ToString("F2",CultureInfo.InvariantCulture)+"来生日":(days*24).ToString("F2",CultureInfo.InvariantCulture)+"来生时";
    }
    public static V summary(Game g) {
        var t=g.s.Get("beta_time",V.Map());var date=calendar(g);
        return V.Map("mode",t.Get("mode","normal"),"acceleration",acceleration(g),"date",$"第{date["year"].Long}年 · {date["month"].Long}月{date["day"].Long}日","calendar",date,"dt",g.s.Get("dt",0),"dt_years",g.s.Get("dt",0).Double*g.c.beta["time"]["earth_seconds_per_dt"].Double/g.c.beta["units"]["earth_seconds_per_year"].Double,"paused",g.s.Get("settings",V.Map()).Get("user_paused",false),"pause_reason",t.Get("pause_reason",""),"local_elapsed_seconds",t.Get("local_elapsed_seconds",0),"active_real_seconds",t.Get("active_real_seconds",0),"offline_recorded_seconds",t.Get("offline_recorded_seconds",0));
    }
    public static double _next_period(double now,double period) {double result=(Math.Floor(now/period+1e-12)+1)*period-now;return result>0?result:period;}
    public static double _boundary_seconds(Game g,double limit) {
        double month=g.c.beta["units"]["earth_seconds_per_month"].Double,now=g.s["beta_time"]["local_elapsed_seconds"].Double,day=g.c.beta["units"]["earth_seconds_per_laisheng_day"].Double;
        double result=Math.Min(limit,_next_period(now,day*g.c.beta["time"]["max_economic_step_laisheng_days"].Double));
        result=Math.Min(result,_next_period(now,g.c.beta["units"]["earth_seconds_per_year"].Double));
        double arrival=(g.s["event_cursor"].Long+1)*g.c.beta["economy_candidates"]["arrival_interval_months"].Double*month-now;
        if(arrival>0)result=Math.Min(result,arrival);
        double capacity=Construction.capacity(g)*Clamp(g.s["metrics"].Get("energy_ratio",1).Double,0,1)
            *(1-Clamp(g.s["block"].Get("city_cp_reserve",0).Double,0,1));
        if(capacity>0)foreach(var row in Construction.queue(g)) {
            if(row["paused"].Bool)continue;double work=row["work"].Double;
            if(work>0)result=Math.Min(result,work/capacity*month);
            double rate=row["material"].Double/Math.Max(1e-15,work)*capacity,stock=g.inventory_total("material");
            if(rate>0&&stock>1e-9)result=Math.Min(result,stock/rate*month);break;
        }
        foreach(string resource in new[]{"food","material","goods"}) {
            double flow=g.s["metrics"].Get("flows",V.Map()).Get(resource,0).Double,stock=g.inventory_total(resource);
            if(flow< -1e-9&&stock>1e-9)result=Math.Min(result,stock/-flow*month);
            if(flow>1e-9){double room=Market._buy_room(g,resource);if(room>1e-9)result=Math.Min(result,room/flow*month);}
        }
        double theme=Themes.next_boundary_seconds(g);if(theme>0)result=Math.Min(result,theme);
        double cycle=Cycles.next_boundary_seconds(g);if(cycle>0)result=Math.Min(result,cycle);
        return result;
    }
    public static V advance(Game g,double active_real_seconds,string mode_id="") {
        ensure(g);var result=V.Map("advanced",0.0,"local_seconds",0.0,"active_real_seconds",0.0,"spent",0.0,"steps",0,"reason","");
        if(active_real_seconds<=0||!Finite(active_real_seconds)||g.s["phase"].Text!="play"){result["reason"]="无效推进或当前不能经营。";return result;}
        if(mode_id!=""){string error=set_mode(g,mode_id);if(error!=""){result["reason"]=error;return result;}}
        if(g.s["settings"]["user_paused"].Bool){string reason=g.s["beta_time"]["pause_reason"].Text;result["reason"]=reason!=""?reason:"城市已暂停。";return result;}
        double month=g.c.beta["units"]["earth_seconds_per_month"].Double,seconds_per_dt=g.c.beta["time"]["earth_seconds_per_dt"].Double,remaining=active_real_seconds;
        while(remaining>0&&g.s["phase"].Text=="play"&&!g.s["settings"]["user_paused"].Bool) {
            double speed=acceleration(g);if(g.s["dt"].Double<=0){speed=1;g.s["beta_time"]["mode"]="normal";}
            double available=remaining*speed;bool limited=false;
            if(speed>1){double budget=g.s["dt"].Double*seconds_per_dt*speed/(speed-1);if(budget<=available){available=budget;limited=true;}}
            double local=_boundary_seconds(g,available);
            if(local<=0||!Finite(local)){result["reason"]="时间边界无效；本段未执行。";break;}
            double roundoff=Math.Max(1e-9,Math.Abs(g.s["beta_time"]["local_elapsed_seconds"].Double)*4e-16);
            if(limited&&available-local<=roundoff)local=available;
            bool final=limited&&local==available;double real=local/speed,months=local/month,spent=Math.Max(0,local-real)/seconds_per_dt;
            V before=g.s,old_compiled=g.compiled,old_cache=g.spatial_cache;
            string failure=Sim.tick(g,months,V.Map("local_seconds",local,"external_seconds",real,"acceleration",speed));
            if(failure=="") {
                g.s["beta_time"]["local_elapsed_seconds"]=before["beta_time"]["local_elapsed_seconds"].Double+local;
                g.s["local_hours"]=g.s["beta_time"]["local_elapsed_seconds"].Double/month;
                g.s["beta_time"]["active_real_seconds"]=before["beta_time"]["active_real_seconds"].Double+real;
                g.s["beta_time"]["segments_committed"]=before["beta_time"].Get("segments_committed",0).Long+1;
                Themes.tick(g,local);Cycles.tick(g,local,before["metrics"].Get("housed",0).Double);
                if(final){spent=before["dt"].Double;g.s["dt"]=0.0;}else g.s["dt"]=Math.Max(0,before["dt"].Double-spent);
                g.s["totals"]["dt_used"]=g.s["totals"].Get("dt_used",0).Double+spent;
                if(before["dt"].Double>0&&g.s["dt"].Double==0)Block.on_dt_zero(g);
                failure=g.validate_state(g.s);
            }
            if(failure!=""){g.s=before;g.compiled=old_compiled;g.spatial_cache=old_cache;g.recording_cycle=false;g.last_error=failure;result["reason"]=failure;break;}
            Civic.finalize(g);g.last_committed_revision=g.revision;
            result["local_seconds"]=result["local_seconds"].Double+local;result["active_real_seconds"]=result["active_real_seconds"].Double+real;
            result["spent"]=result["spent"].Double+spent;result["steps"]=result["steps"].Long+1;
            remaining=Math.Max(0,remaining-real);if(remaining<=active_real_seconds*1e-14)remaining=0;
            if(g.s["settings"]["user_paused"].Bool)result["reason"]=g.s["beta_time"]["pause_reason"];
        }
        result["advanced"]=result["local_seconds"].Double/month;return result;
    }
}
}
