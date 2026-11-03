/** Confirmed by the designer on 2026-10-26. Undefined rules have no defaults here. */
export const WILD_DESIGN = Object.freeze({
 basicCapacity: 2,
 housingResearchCapacity: 2,
 inheritedTierCapacity: 2,
 directAdvancedCapacity: 0,
 ordinaryNodePoints: 1,
 advancementNodePoints: 2,
 slotBasePoints: 3,
 slotPointMultiplier: 3,
 tierOneSlotBasicGoods: 1,
});

/** Base outputs are fixed yardsticks, not live city yields or net income. */
export const WILD_ECONOMY = Object.freeze({
 monthsPerPeriod: 1,
 materialPerPeriod: 10,
 basicGoodsPerPeriod: 1,
 lowIndustrialPerPeriod: 1,
 baseCPPerPeriod: 10,
 tierOneBuildCP: 10,
 ordinaryEffectFraction: 0.1,
 startingLowIndustrial: 2,
});

export const WILD_RESEARCH = Object.freeze({
 firstHousingPoints: 2,
 annualPoints: { 'secondary-school': 1, university: 20, 'research-institute': 20 },
});

export type WildSlotType = 'organization' | 'link' | 'fusion';
export type WildMaterial = 'material' | 'basicGoods' | 'lowIndustrial' | 'industrial' | 'precisionIndustrial';
export type WildCost = Partial<Record<WildMaterial, number>>;

/** Do not mistake the demo's former slot categories for the approved Wild types. */
export const WILD_SLOT_NAMES: Record<WildSlotType, string> = {
 organization: '组织槽', link: '联系槽', fusion: '融合槽',
};

export const WILD_FARM_FORMS = {
 foundation:{name:'农居',tier:1,housing:10,food:10},
 apartment:{name:'公寓',tier:2,housing:20,food:5},
 farm:{name:'农场',tier:2,housing:5,food:20},
} as const;
