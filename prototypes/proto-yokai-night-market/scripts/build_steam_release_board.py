"""Render the read-only source snapshot into one interactive local Canvas."""
from pathlib import Path
import json
ROOT=Path(__file__).resolve().parents[1]
TARGET=Path('C:/Users/Lenovo/.cursor/projects/f-workspace-rogue-prototype-lab/canvases/yokai-steam-release.canvas.tsx')

def main():
    plan=json.loads((ROOT/'release/plan.json').read_text('utf-8'))
    sources=json.loads((ROOT/'release/official-sources.json').read_text('utf-8'))['sources']
    code=r'''import {Stack,Row,Grid,H1,H2,Text,Card,CardHeader,CardBody,Button,Table,Select,TextInput,Divider,Link,useState,useHostTheme,useCanvasAction} from "cursor/canvas";
const plan = __PLAN__;
const sources = __SOURCES__;
const root = __ROOT__;
const status:Record<string,string>={awaiting_user:"待补用户事实",prepared:"范围建议已备",blocked_by_rights_route:"依赖商业路线",not_started_for_publishable_build:"发行包待验证",draft_prepared:"草稿已备",candidate_assets_prepared:"9张候选已制",shot_list_prepared:"镜头清单已备",templates_prepared:"Preview模板已备",not_submitted:"未送审",awaiting_app_and_publishable_build:"等待合法候选与AppID",awaiting_final_product:"待最终产品确定",not_authorized_for_specific_build:"未批准具体发行包",procedure_prepared:"维护流程已备"};
function addDays(value:string,days:number){if(!/^\d{4}-\d{2}-\d{2}$/.test(value))return null;const d=new Date(value+"T00:00:00Z");if(!Number.isFinite(d.getTime())||d.toISOString().slice(0,10)!==value)return null;return new Date(d.getTime()+days*86400000).toISOString().slice(0,10);}
export default function SteamRelease(){
 const t=useHostTheme(),dispatch=useCanvasAction();
 const [owner,setOwner]=useState("all"),[selected,setSelected]=useState("rights");
 const [paid,setPaid]=useState("2026-10-14"),[soon,setSoon]=useState("2026-10-21"),[wait,setWait]=useState("30");
 const waiting=Number(wait),fee=Number.isInteger(waiting)&&waiting>0&&waiting<=180?addDays(paid,waiting):null,coming=addDays(soon,14),earliest=fee&&coming?[fee,coming].sort()[1]:null;
 const visible=plan.steps.filter(s=>owner==="all"||(owner==="user"?s.owner!=="assistant":s.owner!=="user"&&s.owner!=="user_final_approval"));
 const focus=plan.steps.find(s=>s.id===selected)!;
 const role=(s:string)=>s==="assistant"?"我承担":s==="user"?"你本人办理":s==="user_final_approval"?"你最终决定":"我准备，你确认";
 return <Stack gap={20} style={{padding:24,maxWidth:1200,margin:"0 auto",color:t.text.primary,background:t.bg.editor}}>
  <Row justify="space-between" wrap><H1>妖怪夜市开张 · Steam发行</H1><Text tone="secondary" size="small">核对日期 2026-10-14 · 准备阶段</Text></Row>
  <Card><CardHeader>当前包还不能提交发行审核</CardHeader><CardBody><Text weight="semibold">30份恢复业务脚本、91个原作音频，以及恢复的UI/几何来源尚未取得商业放行记录。</Text><Text>本机91个WAV与原作导出哈希一致。取得明确许可，或走独立实现/素材路线；换皮和AI披露都不能替代这一项。</Text><Text tone="secondary" size="small">这是一份来源和准备状态判断，不是法律结论。没有上传、送审、付款或发布游戏。</Text></CardBody></Card>
  <Grid columns="minmax(0,1fr) minmax(0,1fr)" gap={28}>
   <Stack gap={10}><H2>我已经准备</H2><Text>中英文商店短/长介绍、标签与支持表；AI披露草稿；两份原创生成主视觉和9张Steam规格候选；Preview模板、门禁和验收清单。</Text><Text>7项门禁回归测试通过。对当前研究包的门禁正确拒绝；完整发行QA、AppID和Steam安装测试还未具备。</Text><Row wrap><Button onClick={()=>dispatch({type:"openFile",path:root+"/release/README.md"})}>发行准备说明</Button><Button variant="secondary" onClick={()=>dispatch({type:"openFile",path:root+"/release/media/generated/header.png"})}>查看胶囊候选</Button></Row></Stack>
   <Stack gap={10}><H2>只有你本人做</H2><Text>登录与二次验证、真实身份/主体资料、NDA/SDA签署、税务与收款核验、付款/激活应用名额。</Text><Text>再由你决定具体名称、售价、地区、发售日和最终公开发行。代码、素材、测试、商店填写、配置、上传及审核修改由我尽量完成。</Text><Text tone="secondary" size="small">密码、验证码、银行和税号只填官方后台。当前需要补充：商业授权情况、Steamworks阶段及是否有本游戏AppID。</Text></Stack>
  </Grid>
  <Divider/>
  <Row justify="space-between" wrap><H2>全过程与责任分工</H2><Select value={owner} onChange={setOwner} options={[{value:"all",label:"全部工作"},{value:"user",label:"含你需要确认/办理的工作"},{value:"assistant",label:"我可以准备或执行的工作"}]}/></Row>
  <Table headers={["阶段","工作","责任","当前状态"]} rows={visible.map(s=>[String(s.stage),<Button variant="ghost" onClick={()=>setSelected(s.id)}>{s.title}</Button>,role(s.owner),status[s.status]||s.status])} framed={false}/>
  <Stack gap={8} style={{padding:16,background:t.fill.quaternary}}><H2>{focus.title}</H2><Text><Text weight="semibold">我做：</Text>{focus.assistant}</Text><Text><Text weight="semibold">你做：</Text>{focus.user}</Text><Text tone="secondary"><Text weight="semibold">完成条件：</Text>{focus.doneWhen}</Text></Stack>
  <Divider/>
  <H2>日期试算，示例不代表已经缴费或公开</H2>
  <Grid columns={3} gap={16}><Stack gap={6}><Text size="small">假设缴费日期</Text><TextInput value={paid} onChange={setPaid}/></Stack><Stack gap={6}><Text size="small">假设Coming Soon公开日期</Text><TextInput value={soon} onChange={setSoon}/></Stack><Stack gap={6}><Text size="small">缴费等待天数，以后台为准</Text><TextInput type="number" value={wait} onChange={setWait}/></Stack></Grid>
  {earliest&&<Text weight="semibold">仅这两项日期门槛的较晚日：{earliest}</Text>}
  <Text tone="secondary" size="small">官方入驻文档写21天、Steam Direct入口写30天，暂按30天保守试算；Coming Soon至少14天，两者可并行。审核通常3–5工作日，建议至少预留7工作日处理修改。真正能发售还取决于来源、质量、审核、定价和后台资格，不能把试算当承诺。</Text>
  <H2>官方依据</H2><Row wrap gap={12}>{sources.map(s=><Link href={s.url}>{s.id}</Link>)}</Row>
  <Text tone="tertiary" size="small">来源：本机v2.8工程与发行门禁、Steamworks官方文档（2026-10-14）。9张图为宣传插画候选；真实玩法截图/录像待可发行包完成后制作。当前游戏内82纹理及Player字节未改。</Text>
 </Stack>;
}
'''
    code=code.replace('__PLAN__',json.dumps(plan,ensure_ascii=False)).replace('__SOURCES__',json.dumps(sources,ensure_ascii=False)).replace('__ROOT__',json.dumps(ROOT.as_posix()))
    TARGET.write_text(code,encoding='utf-8',newline='\n')
    print(str(TARGET))
if __name__=='__main__':main()
