import type { WildSession } from './game/wild-runtime';
export type Resource = 'food' | 'material' | 'goods' | 'currency';
export type Service = 'medical' | 'education' | 'entertainment' | 'commercial';
export type BuildingKind = 'farm' | 'center' | 'workshop';
export type Workspace = 'residents' | 'economy' | 'construction' | 'tasks' | 'bonus' | 'archive' | 'system';
export type ScenarioId = 'first' | 'normal' | 'shortage' | 'construction' | 'reward' | 'ending' | 'readonly';
export interface Slot { id: string; kind: 'production' | 'operations' | 'logistics'; bonusId?: string }
export interface Building {
 id: string; name: string; kind: BuildingKind; tier: number; specialization: string; operator: string; running: boolean;
 population: number; employed: number; housing: number; jobs: number; filledJobs: number; health: number; happiness: number;
 services: Record<Service, number>; pollution: number; heat: number; powerSupply: number; powerDemand: number;
 flows: Record<Resource, number>; income: number; maintenance: number; slots: Slot[];
}
export interface Tile { id: string; q: number; r: number; x: number; y: number; terrain: string; features: string[]; buildable: boolean; building?: Building; reach: Record<Service, number>; pollution: number; heat: number }
export interface Project { wildNode?: string; wildAdvance?: boolean; id: string; tileId: string; type: 'new' | 'upgrade'; kind: BuildingKind; specialization: string; operator: string; targetTier: number; totalCP: number; doneCP: number; totalMaterial: number; usedMaterial: number; priority: number; paused: boolean }
export interface Crew { id: string; cp: number; remainingMonths: number }
export interface Order { counterparty?: 'hope'; id: string; resource: Resource; side: 'buy' | 'sell'; quantity: number; price: number; status: 'open' | 'filled' | 'cancelled' }
export interface Contract { id: string; title: string; description: string; resource: Resource; quantity: number; reward: number; deadline: number; status: 'available' | 'active' | 'completed' | 'expired' }
export interface Reward { id: string; source: string; choices: string[] }
export interface RecordEntry { id: string; category: 'event' | 'settlement' | 'achievement' | 'rule'; title: string; text: string; month: number; tileId?: string }
export interface GameState {
 wild?: WildSession;
 scenario: ScenarioId; name: string; origin: string; theme: string; month: number; dt: number; speed: number; paused: boolean; settled: boolean; readonly: boolean;
 migrationPaused: boolean; goodsActive: boolean; resources: Record<Resource, number>; tiles: Tile[]; projects: Project[]; crews: Crew[];
 bonuses: Record<string, number>; bonusLevels: Record<string, number>; orders: Order[]; policy: string;
 stage: number; holdMonths: number; completedStages: number[]; contracts: Contract[]; rewards: Reward[]; records: RecordEntry[];
 named: boolean; capstone: string; nextId: number;
}
export interface BonusDef { id: string; name: string; kind: Slot['kind']; description: string; effect: string; target: BuildingKind[]; delta: Partial<Record<Resource | Service | 'powerSupply' | 'powerDemand' | 'pollution' | 'housing', number>>; source: string }
export interface PolicyDef { id: string; name: string; description: string; cost: number; effects: string[]; net: Partial<Record<Resource, number>> }
export interface Summary { population: number; employed: number; housing: number; jobs: number; filledJobs: number; health: number; happiness: number; services: Record<Service, number>; net: Record<Resource, number>; cpBase: number; cpBuilding: number; cpCrews: number; cpTotal: number; cpUsed: number; cpReserved: number; powerSupply: number; powerDemand: number; foodRatio: number }
export interface Condition { id: string; label: string; current: number; target: number; unit: string; met: boolean; help: string; destination: Workspace; tab?: string }
export interface ComparisonRow { label: string; before: number; after: number; unit: string; lowerBetter?: boolean }
export interface DemoAction { type: string; [key: string]: any }
export interface CommandResult { state: GameState; ok: boolean; message: string }
export interface ModalState { kind: 'build' | 'upgrade' | 'reward' | 'crew' | 'newgame' | 'star'; tileId?: string; buildingKind?: BuildingKind; rewardId?: string }
export interface PanelMemory { tab: string; filter: string; selected: string; sort: string; scroll: number; advancedScroll?: number; draft?: Record<string,string> }
