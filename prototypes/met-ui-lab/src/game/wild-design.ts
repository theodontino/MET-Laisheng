import { createDesign, advanceTier, designCapacity, designUsed, type DesignState, type DesignRules } from './star';
import { WILD_DESIGN, type WildSlotType } from './wild-config';

/** Advanced direct builds receive no starting or skipped-Tier design allowance. */
export function createWildDesign(entryTier: number): DesignState {
 return createDesign(entryTier, entryTier === 1 ? WILD_DESIGN.basicCapacity : WILD_DESIGN.directAdvancedCapacity);
}

/** Both old and newly built buildings read the same completed research count. */
export function wildDesignRules(completedHousingResearch: number): DesignRules {
 return { technologyPoints: completedHousingResearch * WILD_DESIGN.housingResearchCapacity, slotBasePoints: WILD_DESIGN.slotBasePoints };
}

export interface WildBonusIdentity {
 id: string;
 category: WildSlotType;
 buildingTypes: readonly string[];
}
export interface BonusMatch { allowed: boolean; reason: string }

/** Fusion eligibility must be supplied explicitly until its allowed catalog is confirmed. */
export function matchWildBonus(
 slot: WildSlotType, buildingType: string, bonus: WildBonusIdentity,
 fusionEligibleIds?: readonly string[],
): BonusMatch {
 if (slot === 'fusion') {
  if (!fusionEligibleIds) return { allowed: false, reason: '融合可装配范围待确认' };
  return fusionEligibleIds.includes(bonus.id)
   ? { allowed: true, reason: '融合槽允许此 Bonus 跨建筑类型装配；费用另行报价' }
   : { allowed: false, reason: '此 Bonus 不在融合装配范围内' };
 }
 if (bonus.category !== slot) return { allowed: false, reason: 'Bonus 与专用槽类型不匹配' };
 if (!bonus.buildingTypes.includes(buildingType)) return { allowed: false, reason: '此建筑类型需要通过融合槽装配该 Bonus' };
 return { allowed: true, reason: '槽位与建筑类型匹配' };
}

export interface FormCompletion { formId: string; fromTier: number; toTier: number; completed: boolean }
/** Call on actual upgrade completion, never on quoting, previewing or starting construction. */
export function unlockCompletedForm(unlocked: readonly string[], event: FormCompletion): string[] {
 if (!event.completed || event.toTier !== event.fromTier + 1) return [...unlocked];
 return unlocked.includes(event.formId) ? [...unlocked] : [...unlocked, event.formId];
}

/** The irreversible progression node keeps occupying two points after completion. */
export function completeWildAdvancement(design: DesignState, rules: DesignRules): DesignState {
 if (designUsed(design) + WILD_DESIGN.advancementNodePoints > designCapacity(design, rules))
  throw new Error('进阶需要空出 2 点设计额度');
 const nextTier = design.currentTier + 1;
 const advanced = advanceTier(design, nextTier, WILD_DESIGN.inheritedTierCapacity);
 return { ...advanced, advancementPointCosts: {
  ...design.advancementPointCosts, [`tier-${nextTier}`]: WILD_DESIGN.advancementNodePoints,
 } };
}
