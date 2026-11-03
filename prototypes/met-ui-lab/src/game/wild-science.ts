import type { CommandResult, GameState } from '../types';
import { advanceWildResearch, housingResearchQuote, researchRate, type WildSession } from './wild-runtime';

export const CITY_YEAR_REAL_DAYS = 216;
export const CITY_MONTH_REAL_MS = 18 * 24 * 60 * 60 * 1000;
export const SCIENCE_CITY_MONTHS_PER_DT = .8;

type ClockSession = WildSession & { lastCityScienceAtMs?: number };

/** Wall-clock science also runs while the block is paused or the app is closed. */
export function syncCityScience(input: GameState, nowMs: number): GameState {
 if (!input.wild || input.readonly || !Number.isFinite(nowMs)) return input;
 const baseline = (input.wild as ClockSession).lastCityScienceAtMs;
 // Keep the high-water timestamp if the system clock moves backwards.
 if (baseline !== undefined && nowMs <= baseline) return input;
 const state = structuredClone(input);
 (state.wild as ClockSession).lastCityScienceAtMs = nowMs;
 if (baseline !== undefined) advanceWildResearch(state, (nowMs - baseline) / CITY_MONTH_REAL_MS);
 return state;
}

export interface ScienceDTQuote {
 allowed: boolean;
 reason: string;
 requestedDT: number;
 dtSpent: number;
 cityMonths: number;
 pointsAdded: number;
}

/** Quote only the useful fraction of DT up to the current technology's target. */
export function quoteScienceDT(input: GameState, dtAmount: number): ScienceDTQuote {
 const base: ScienceDTQuote = { allowed: false, reason: '', requestedDT: dtAmount, dtSpent: 0, cityMonths: 0, pointsAdded: 0 };
 if (input.readonly) return { ...base, reason: '该街区已归档，只能查阅' };
 if (!Number.isFinite(dtAmount) || dtAmount <= 0) return { ...base, reason: '科研 DT 必须为正数' };
 const price = housingResearchQuote(input), progress = input.wild?.researchProgress;
 if (progress === undefined || progress === null || !price) return { ...base, reason: '请先启动住房研究' };
 const rate = researchRate(input);
 if (rate <= 0) return { ...base, reason: '需要运行中的中学、大学或研究所提供研究点' };
 const remaining = Math.max(0, price.targetPoints - progress);
 if (remaining === 0) return { ...base, reason: '当前研究已达到目标' };
 const dtSpent = Math.min(dtAmount, remaining / (rate * SCIENCE_CITY_MONTHS_PER_DT));
 const cityMonths = dtSpent * SCIENCE_CITY_MONTHS_PER_DT;
 if (input.dt < dtSpent) return { ...base, reason: '可用 DT 不足' };
 return { allowed: true, reason: '仅推进城市科研；不推进街区月份或工程', requestedDT: dtAmount,
  dtSpent, cityMonths, pointsAdded: Math.min(remaining, rate * cityMonths) };
}

export interface ScienceDTResult extends CommandResult {
 dtSpent: number;
 cityMonths: number;
 pointsAdded: number;
}

export function spendScienceDT(input: GameState, dtAmount: number): ScienceDTResult {
 const quote = quoteScienceDT(input, dtAmount);
 if (!quote.allowed) return { state: input, ok: false, message: quote.reason, dtSpent: 0, cityMonths: 0, pointsAdded: 0 };
 const state = structuredClone(input);
 state.dt -= quote.dtSpent;
 advanceWildResearch(state, quote.cityMonths);
 state.records.unshift({ id: `r${state.nextId++}`, category: 'event', title: '科研 DT 投入', month: state.month,
  text: `使用 ${quote.dtSpent} DT 推进 ${quote.cityMonths} 城市月科研，获得 ${quote.pointsAdded} 研究点；街区月份、工程和资源不变。` });
 return { state, ok: true, message: '科研 DT 已投入', dtSpent: quote.dtSpent, cityMonths: quote.cityMonths, pointsAdded: quote.pointsAdded };
}
