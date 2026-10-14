"""Embed versioned migration planning data in a single host-native Canvas."""
from pathlib import Path
import json
ROOT=Path(__file__).resolve().parents[1]
PLAN=ROOT/'release/commercial-migration-v3'
TARGET=Path('C:/Users/Lenovo/.cursor/projects/f-workspace-rogue-prototype-lab/canvases/yokai-commercial-migration.canvas.tsx')
def main():
    def read(name):return json.loads((PLAN/name).read_text('utf-8'))
    summary=read('audit-summary.json');plan=read('execution-plan.json');assets=read('asset-plan.json');symbols=read('symbol-plan.json');tests=read('test-plan.json')
    rows=[dict(id=a['id'],old=a['oldName'],target=a['proposedName'],group='图片',kind=a['category'],spec=' × '.join(map(str,a['dimensions'])),action=a['method']) for a in assets['textures']]
    rows += [dict(id=a['id'],old=a['oldName'],target=a['proposedName'],group='音频',kind=a['group'],spec=f"{a['spec']['sampleRate']} Hz · {a['spec']['channels']}声道 · {a['spec']['durationSeconds']:.3f}秒",action=a['method']) for a in assets['audio']]
    code=r'''import {Stack,Row,Grid,H1,H2,Text,Button,Table,Select,TextInput,Divider,Link,useState,useHostTheme,useCanvasAction} from "cursor/canvas";
const summary=__SUMMARY__;
const plan=__PLAN__;
const assets=__ASSETS__;
const types=__TYPES__;
const tests=__TESTS__;
const root=__ROOT__;
export default function CommercialMigration(){
 const t=useHostTheme(),dispatch=useCanvasAction();
 const [view,setView]=useState('phases'),[focus,setFocus]=useState('M0'),[group,setGroup]=useState('全部'),[query,setQuery]=useState(''),[page,setPage]=useState(0);
 const phase=plan.phases.find(p=>p.id===focus)!;
 const filtered=assets.filter(a=>(group==='全部'||a.group===group)&&JSON.stringify(a).toLowerCase().includes(query.toLowerCase()));
 const pages=Math.max(1,Math.ceil(filtered.length/15)),safePage=Math.min(page,pages-1),visible=filtered.slice(safePage*15,safePage*15+15);
 return <Stack gap={20} style={{padding:24,maxWidth:1250,margin:'0 auto',color:t.text.primary,background:t.bg.editor}}>
 <Row justify="space-between" wrap><H1>妖怪夜市 · 商业版本迁移</H1><Text size="small" tone="secondary">2026-10-14 · 分析与方案，尚未实施</Text></Row>
 <Stack gap={8} style={{padding:16,background:t.fill.quaternary}}><Text weight="semibold">用户已声明授权。新要求是资产零原作复用、夜市结构与命名、当前流程不变。</Text><Text>当前91音轨仍是原作字节，82张夜市图仍需独立alpha/网格/引用链。4原TXT、12原TTF也要排除。旧版967输入与Player未改。</Text><Text size="small" tone="secondary">新资源锁尺寸、有效尺度/锚点、点击、排序与时序；不再复制原alpha、顶点、YAML或meta。保留私有来源历史和必要许可声明。</Text></Stack>
 <Grid columns={3} gap={24}>
 <Stack gap={5}><H2>素材</H2><Text>82图 · 91音轨 · 25字体输入</Text><Text size="small" tone="secondary">另含48材质、78 Sprite、18 prefab与场景。42PNG/13OBJ属于恢复导出的Unity内置资源副本。</Text></Stack>
 <Stack gap={5}><H2>代码与数据</H2><Text>32主类型 · 95卡 · 28委托 · 420键</Text><Text size="small" tone="secondary">含嵌套共39类型；664方法、946字段、62属性、285参数有草案。语义改名尚未执行。</Text></Stack>
 <Stack gap={5}><H2>验证</H2><Text>8实施阶段 · 31组验收定义</Text><Text size="small" tone="secondary">当前仅完成方案一致性检查。748画面为后续四组合回归目标；完整原生/听感仍须执行。</Text></Stack>
 </Grid>
 <Row wrap><Button onClick={()=>dispatch({type:'openFile',path:root+'/README.md'})}>打开完整方案</Button><Button variant="secondary" onClick={()=>dispatch({type:'openFile',path:root+'/asset-plan.json'})}>逐项资产规格</Button><Button variant="secondary" onClick={()=>dispatch({type:'openFile',path:root+'/symbol-plan.json'})}>全部符号草案</Button><Button variant="secondary" onClick={()=>dispatch({type:'openFile',path:root+'/data-plan.json'})}>数据映射</Button></Row>
 <Divider/>
 <Row wrap>{[['phases','实施顺序'],['assets','173项图片与音频'],['types','类型与职责'],['risks','风险与边界'],['tests','验收定义']].map(([id,label])=><Button variant={view===id?'primary':'ghost'} onClick={()=>setView(id)}>{label}</Button>)}</Row>
 {view==='phases'&&<Stack gap={16}><Table headers={['阶段','任务','依赖','状态']} rows={plan.phases.map(p=>[p.id,<Button variant="ghost" onClick={()=>setFocus(p.id)}>{p.title}</Button>,p.dependsOn.join('、')||'基线入口','尚未实施'])} framed={false}/><Stack gap={8} style={{padding:16,background:t.fill.quaternary}}><H2>{phase.id} · {phase.title}</H2>{phase.actions.map(a=><Text>{a}</Text>)}<Text weight="semibold">退出条件</Text>{phase.exit.map(a=><Text tone="secondary">{a}</Text>)}<Text size="small" tone="secondary">回滚：{phase.rollback}</Text></Stack></Stack>}
 {view==='assets'&&<Stack gap={14}><Row wrap justify="space-between"><H2>逐项制作输入</H2><Row wrap><Select value={group} onChange={v=>{setGroup(v);setPage(0);}} options={['全部','图片','音频'].map(v=>({value:v,label:v}))}/><TextInput value={query} onChange={v=>{setQuery(v);setPage(0);}} placeholder="搜索原槽位、新名或制作方法"/></Row></Row><Text size="small" tone="secondary">符合筛选 {filtered.length} 项。尺寸/格式来自当前源；原名只用于迁移映射，最终产品不保留。表内命名为候选，全部尚未替换。</Text>{visible.length>0&&<Table headers={['槽位','旧名 → 新名','类型与规格','制作方法']} rows={visible.map(a=>[a.id,a.old+' → '+a.target,a.group+' / '+a.kind+' / '+a.spec,a.action])}/>}{filtered.length>15&&<Row justify="space-between"><Button variant="secondary" disabled={safePage===0} onClick={()=>setPage(safePage-1)}>上一页</Button><Text size="small">{safePage+1} / {pages}</Text><Button variant="secondary" disabled={safePage>=pages-1} onClick={()=>setPage(safePage+1)}>下一页</Button></Row>}</Stack>}
 {view==='types'&&<Stack gap={14}><H2>保留职责后再逐步拆分</H2><Text tone="secondary">先处理类型工厂、节点/资源绑定、序列化和保存。随后拆唯一规则、会话与表现；不同时换算法。Unity生命周期、引擎协议和必要许可名保留。</Text><Table headers={['旧类型','新类型','方法 / 字段','职责']} rows={types.map(a=>[a.old,a.proposed,String(a.methods)+' / '+String(a.fields),a.responsibility])}/><Text size="small" tone="secondary">方法/字段/属性/参数全表见符号草案；编译器/Unity生成类型不作为业务改名对象。token/blob用于旧重载身份，后续以语义签名映射比较。</Text></Stack>}
 {view==='risks'&&<Stack gap={14}><H2>保持体验的边界</H2><Table headers={['风险','处理方法']} rows={plan.risks.map(r=>[r.title,r.mitigation])}/><Text>抽牌逐次取索引，不改洗牌；AI次序不变；布符目标2；调停5.99秒；同帧队列、快进1.5与保存均差分验证。比较基线是当前Unity 2022版本。</Text><Text tone="secondary">零相同hash不是来源独立证明。每项要有独立输入谱系；新增alpha/网格的视觉差异逐项审阅，不宣称与旧图像素相同。当前源码摘要：{summary.baselineSourceSHA256}</Text></Stack>}
 {view==='tests'&&<Stack gap={14}><H2>31组验收定义 · 尚未执行</H2><Table headers={['编号','范围','触发场景','通过条件']} rows={tests.cases.map(c=>[c.id,c.group,c.scenario,c.expected])}/></Stack>}
 <Divider/><Text size="small" tone="tertiary">来源：本机当前v2.8工程/导出/素材谱系、既有Release符号提取，2026-10-14只读盘点。计数命中不等于运行覆盖。授权按用户声明登记，文书未审阅；发行与新版完整验收均未完成。</Text>
 <Row wrap><Link href="https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Serialization.FormerlySerializedAsAttribute.html">Unity字段迁移</Link><Link href="https://docs.unity3d.com/2022.3/Documentation/Manual/AssetMetadata.html">Unity资产元数据</Link></Row>
 </Stack>;
}
'''
    for key,value in [('SUMMARY',summary),('PLAN',plan),('ASSETS',rows),('TYPES',symbols['types']),('TESTS',tests),('ROOT',PLAN.as_posix())]:
        code=code.replace('__'+key+'__',json.dumps(value,ensure_ascii=False))
    TARGET.write_text(code,encoding='utf-8',newline='\n');print(TARGET)
if __name__=='__main__':main()
