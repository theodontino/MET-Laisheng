import { createContext, useContext } from 'react';
import type { GameState, Summary, Workspace, PanelMemory, DemoAction, ModalState, ScenarioId } from './types';
export interface LabAPI {
 game:GameState; stats:Summary; workspace:Workspace|null; selectedTile:string; view:PanelMemory;
 act:(action:DemoAction)=>boolean; go:(workspace:Workspace,tab?:string,selected?:string,draft?:Record<string,string>)=>void;
 advanced:(workspace?:Workspace,tab?:string,selected?:string,draft?:Record<string,string>)=>void;
 showLayer:(layer:string)=>void;
 remember:(patch:Partial<PanelMemory>)=>void; inspect:(tileId:string)=>void; locate:(tileId:string)=>void;
 openModal:(modal:ModalState)=>void; notice:(message:string)=>void; setScenario:(id:ScenarioId)=>void;
 settings:{uiScale:number;fontScale:number}; setSettings:(patch:Partial<{uiScale:number;fontScale:number}>)=>void;
 exportWeb:()=>void; importWeb:(file:File)=>Promise<void>; restoreAuto:()=>void;
 saveDemo:()=>void; loadDemo:()=>void; resetDemo:()=>void; trackTask:()=>void;
}
export const LabContext = createContext<LabAPI|null>(null);
export function useLab(){const value=useContext(LabContext);if(!value)throw new Error('Lab provider missing');return value;}
