import type { WildCost } from './wild-config';

/** Applies only to construction work, never to resource or research prices. */
export const UPGRADE_CP_PER_DESIGN_POINT = 0.1;

/**
 * Uses this node's occupancy, not total building capacity or total occupied points.
 * The caller supplies the building's current Tier construction work in CP.
 */
export function upgradeConstructionCP(currentTierBuildCP: number, designPoints: number): number {
 return currentTierBuildCP * UPGRADE_CP_PER_DESIGN_POINT * designPoints;
}

/** Resource prices are required explicitly and stay separate from the CP formula. */
export function quoteNodeInstallation(currentTierBuildCP: number, designPoints: number, resourceCost: WildCost) {
 return { designPoints, constructionCP: upgradeConstructionCP(currentTierBuildCP, designPoints), resourceCost: { ...resourceCost } };
}
