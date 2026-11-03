import type { GameState } from '../types';

export const SAVE_KEY = 'met-wild-web-save-v1';
export const AUTO_SAVE_KEY = 'met-wild-web-auto-v1';
type SaveSettings = { uiScale: number; fontScale: number };
type RestoredSave = { game: GameState; settings: SaveSettings; savedAt: string };
const invalid = (detail: string): never => { throw new Error(`存档内容无效：${detail}。请重新选择完整的 Web 游戏存档。`); };
const object = (value: unknown): value is Record<string, unknown> => !!value && typeof value === 'object' && !Array.isArray(value);
const nonnegative = (value: unknown): value is number => typeof value === 'number' && Number.isFinite(value) && value >= 0;

function validateGame(value: unknown): asserts value is GameState {
 if (!object(value)) invalid('缺少城市数据');
 const game = value as Record<string, unknown>;
 if (typeof game.name !== 'string' || !nonnegative(game.month) || !Number.isInteger(game.month) || !nonnegative(game.dt)) invalid('城市名称、月份或 DT 不正确');
 if (!object(game.resources) || !['food', 'material', 'goods', 'currency'].every(key => nonnegative((game.resources as Record<string, unknown>)[key]))) invalid('资源库存必须为有限非负数');
 for (const key of ['tiles', 'projects', 'crews', 'orders', 'completedStages', 'contracts', 'rewards', 'records']) {
  if (!Array.isArray(game[key])) invalid(`缺少 ${key} 列表`);
 }
 for (const key of ['bonuses', 'bonusLevels']) if (!object(game[key])) invalid(`缺少 ${key} 数据`);
 if (!nonnegative(game.nextId) || !Number.isInteger(game.nextId) || typeof game.paused !== 'boolean') invalid('运行状态不完整');

 const tileIds = new Set<string>();
 for (const tile of game.tiles as unknown[]) {
  if (!object(tile) || typeof tile.id !== 'string' || !tile.id || tileIds.has(tile.id)) invalid('地块编号缺失或重复');
  tileIds.add((tile as Record<string, unknown>).id as string);
 }
 if (!tileIds.size) invalid('城市没有地块');
 const projectIds = new Set<string>();
 const projectTiles = new Set<string>();
 for (const project of game.projects as unknown[]) {
  if (!object(project)) invalid('施工项目不完整');
  const item = project as Record<string, unknown>;
  if (typeof item.id !== 'string' || !item.id || projectIds.has(item.id)) invalid('施工项目编号缺失或重复');
  if (typeof item.tileId !== 'string' || !tileIds.has(item.tileId) || projectTiles.has(item.tileId)) invalid('施工项目的地块不存在或重复占用');
  projectIds.add(item.id as string);
  projectTiles.add(item.tileId as string);
 }
}

function validateSettings(value: unknown): asserts value is SaveSettings {
 if (!object(value) || !['uiScale', 'fontScale'].every(key => typeof value[key] === 'number' && Number.isFinite(value[key]) && (value[key] as number) > 0)) invalid('界面比例不正确');
}

export function encodeSave(game: GameState, settings: SaveSettings): string {
 validateGame(game);
 validateSettings(settings);
 return JSON.stringify({ format: 'met-wild-web', version: 1, savedAt: new Date().toISOString(), game, settings });
}

export function decodeSave(text: string): RestoredSave {
 let envelope: unknown;
 try { envelope = JSON.parse(text); }
 catch { throw new Error('无法读取存档：文件不是有效 JSON。请重新选择导出的 Web 游戏存档。'); }
 if (!object(envelope) || envelope.format !== 'met-wild-web') throw new Error('这不是 Web 游戏存档。旧演示、Unity 和 Godot 存档不能导入；请使用 Web 游戏导出的存档。');
 if (envelope.version !== 1) throw new Error(`暂不支持存档版本 ${String(envelope.version)}。请用导出它的 Web 游戏版本打开，或选择版本 1 存档。`);
 if (typeof envelope.savedAt !== 'string' || !Number.isFinite(Date.parse(envelope.savedAt))) invalid('保存时间不正确');
 validateGame(envelope.game);
 validateSettings(envelope.settings);
 // Keep the full parsed game, including fields added by later gameplay modules.
 return { game: { ...envelope.game, paused: true }, settings: envelope.settings, savedAt: envelope.savedAt as string };
}
