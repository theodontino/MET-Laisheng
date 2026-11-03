using UnityEngine;

namespace MET.City
{
    /// <summary>
    /// Presentation-only illustration routing, sourced from StreamingAssets/MetContent/core020.json
    /// (10 buildings, all 44 bonuses) and 040/buildings.json (21 buildings).
    /// Twenty purpose-specific compositions are intentionally reused by related definitions.
    /// Pass the definition's stable string ID, never its localized display name.
    /// </summary>
    public static class CityIllustrationCatalog
    {
        public const int Industrial = 0;
        public const int Residential = 1;
        public const int Civic = 2;
        public const int Farm = 3;
        public const int Clinic = 4;
        public const int School = 5;
        public const int Market = 6;
        public const int Energy = 7;
        public const int Logistics = 8;
        public const int Park = 9;
        public const int Extraction = 10;
        public const int Research = 11;
        public const int Workshop = 12;
        public const int HeatNetwork = 13;
        public const int Recycling = 14;
        public const int Mobility = 15;
        public const int Kitchen = 16;
        public const int DenseHousing = 17;
        public const int Utilities = 18;
        public const int Modular = 19;
        public const int StyleCount = 20;

        public static int StyleForBuilding(string definitionId)
        {
            switch (definitionId)
            {
                case "works": case "manufacturing": case "material_factory": return Industrial;
                case "hamlet": case "residence": return Residential;
                case "homestead": case "farm": case "agriculture": case "modern_agriculture": return Farm;
                case "neighborhood": case "public_center": return Civic;
                case "clinic": case "medical_clinic": case "hospital": return Clinic;
                case "school": case "primary_school": case "secondary_school": return School;
                case "market": case "commercial_center": return Market;
                case "power": case "power_station": case "power_plant": return Energy;
                case "warehouse": case "yard": return Logistics;
                case "park": case "garden": return Park;
                case "timber": case "extraction": return Extraction;
                case "workshop": return Workshop;
                case "advanced_residence": return DenseHousing;
                case "freight": return Mobility;
                default: return Civic;
            }
        }

        /// <summary>All 44 existing Bonus IDs are listed explicitly, grouped by reusable visual category.</summary>
        public static int StyleForBonus(string bonusId)
        {
            switch (bonusId)
            {
                // Factory / assembly: 粗野制造、零库存生产、东郊记忆、污染产业园、企业内部协同 (5)
                case "P01": case "L03": case "O06": case "B08": case "G01": return Industrial;
                // 工厂家属区 (1)
                case "O02": return Residential;
                // 邻里中心、服务集中 (2)
                case "L04": case "B01": return Civic;
                // 家庭农场、都市农业、农家乐、精耕小农 (4)
                case "P07": case "R10": case "R14": case "R15": return Farm;
                // 商业医疗、社区诊所、邻里照护网络 (3)
                case "O01": case "R05": case "O08": return Clinic;
                // 实践教育、开放大学、义务教师、街坊夜校 (4)
                case "P03": case "O04": case "O05": case "B10": return School;
                // 商业综合体、联合采购、社区商业 (3)
                case "O07": case "B07": case "R08": return Market;
                // 生物质能公共设施、用爱发电 (2)
                case "P04": case "B06": return Energy;
                // 电商仓储店、中转仓、战略储备、内部供应链 (4)
                case "L01": case "L05": case "B05": case "G02": return Logistics;
                // 厂区防护缓冲 (1)
                case "R19": return Park;
                // 研究型医院 (1): observatory / laboratory study, distinct from ordinary care.
                case "P02": return Research;
                // 家庭作坊、职住一体 (2)
                case "R16": case "R17": return Workshop;
                // 热量管理、市政暖气、热电共生 (3)
                case "P05": case "B02": case "B09": return HeatNetwork;
                // 高级回收 (1)
                case "P06": return Recycling;
                // 无人机站、地下快速路 (2)
                case "L02": case "B04": return Mobility;
                // 公共厨房 (1)
                case "R18": return Kitchen;
                // 密集住宅 (1)
                case "O03": return DenseHousing;
                // 水基工厂、市政管廊 (2)
                case "P08": case "B03": return Utilities;
                // 模块化设计、近邻装配 (2)
                case "M01": case "P09": return Modular;
                default: return Civic;
            }
        }

        public static Color AccentForBuilding(string definitionId) { return AccentForStyle(StyleForBuilding(definitionId)); }
        public static Color AccentForBonus(string bonusId) { return AccentForStyle(StyleForBonus(bonusId)); }
        public static Color AccentForStyle(int style)
        {
            switch (style)
            {
                case Industrial: case Workshop: return new Color(.86f,.66f,.42f);
                case Residential: case DenseHousing: return new Color(.78f,.67f,.48f);
                case Farm: case Park: return new Color(.64f,.75f,.43f);
                case Clinic: return new Color(.76f,.54f,.43f);
                case School: case Research: return new Color(.65f,.76f,.78f);
                case Market: case Kitchen: return new Color(.88f,.73f,.44f);
                case Energy: case HeatNetwork: return new Color(.88f,.66f,.37f);
                case Logistics: case Mobility: return new Color(.65f,.75f,.74f);
                case Extraction: return new Color(.76f,.66f,.51f);
                case Recycling: return new Color(.62f,.76f,.61f);
                case Utilities: return new Color(.56f,.73f,.76f);
                case Modular: return new Color(.77f,.76f,.60f);
                default: return new Color(.79f,.78f,.62f);
            }
        }

        public static string NameForStyle(int style)
        {
            switch(style)
            {
                case Industrial: return "工业装配";
                case Residential: return "院落住宅";
                case Civic: return "公共市政";
                case Farm: return "田垄温室";
                case Clinic: return "诊所照护";
                case School: return "钟楼学校";
                case Market: return "市集摊棚";
                case Energy: return "风能光伏";
                case Logistics: return "仓储货场";
                case Park: return "街坊公园";
                case Extraction: return "台地采掘";
                case Research: return "研究观测";
                case Workshop: return "工坊工作台";
                case HeatNetwork: return "锅炉热网";
                case Recycling: return "循环回收";
                case Mobility: return "交通转运";
                case Kitchen: return "共享厨房";
                case DenseHousing: return "多层住宅";
                case Utilities: return "地下管廊";
                case Modular: return "模块装配";
                default: return "公共市政";
            }
        }
    }
}
