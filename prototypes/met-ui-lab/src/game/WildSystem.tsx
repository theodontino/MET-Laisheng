import { useRef } from 'react';
import { useLab } from '../context';
import { Button, SectionTitle, InlineConfirm, Tag } from '../ui';

export default function WildSystem(){
 const {game,selectedTile,act,go,openModal,exportWeb,importWeb,restoreAuto}=useLab();
 const file=useRef<HTMLInputElement>(null),s=game.wild;
 const target=game.tiles.find(t=>t.id===selectedTile&&t.building?.kind==='farm')||game.tiles.find(t=>t.building?.kind==='farm')||game.tiles.find(t=>t.building);
 

 return <section className="card" id="system-wild"><SectionTitle aside={<Tag>{s?'规则已接入':'独立规则接入'}</Tag>}>Wild · 建筑星图与城市研究</SectionTitle>
  <p>在现有 UI 中使用新星图。已确认规则真实执行；农居 T1 节点和 T2 公寓／农场已接通价格、施工与继承；其他建筑和后续 Tier 仍待确定。原经营、市场与任务仍沿用 UI demo 模型，当前不是完整 Wild 游戏。</p>
  {!s?<InlineConfirm label="启用 Wild 星图" disabled={game.readonly} onConfirm={()=>act({type:'wild-enable'})}><p>给当前建筑建立独立设计档案。高级建筑按当前 Tier 起步，不补造继承历史。现有工程须先完成；开始时获得 2 件低级工业品。旧 demo 存档不会被覆盖。</p></InlineConfirm>:<>
   <div className="actions"><Button variant="primary" disabled={!target} onClick={()=>target&&openModal({kind:'star',tileId:target.id})}>查看{target?.building?.name||'建筑'}星图</Button><span>基础额度 2 · 每次科技 +2 · 升阶赠点与占点均为 2</span></div>
   <Button onClick={()=>go('tasks','science')}>前往任务与成长 · 科技</Button>
  </>}
  <SectionTitle>Web 存档文件</SectionTitle><p>导出文件可跨浏览器保留当前状态。导入替换当前状态并暂停时间；旧 demo、Unity 与 Godot 的原存档不在这里修改。</p>
  <div className="actions"><Button onClick={exportWeb}>导出 Web 存档</Button><Button onClick={()=>file.current?.click()}>导入并替换当前状态</Button><InlineConfirm label="恢复 Wild 自动存档" onConfirm={restoreAuto}><p>用最近一次 Wild 自动保存替换当前状态。</p></InlineConfirm></div>
  <input ref={file} type="file" accept=".json,application/json" hidden aria-label="导入 Web 存档文件" onChange={e=>{const chosen=e.currentTarget.files?.[0];if(chosen)void importWeb(chosen);e.currentTarget.value=''}}/>
 </section>
}
