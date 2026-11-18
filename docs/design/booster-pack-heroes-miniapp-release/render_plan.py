"""Render the archived Markdown and canonical JSON as an offline planning workbook.

No third-party packages or network access. This renders documentation only.
"""
from __future__ import annotations

import hashlib
import html
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parent


def inline(value: str) -> str:
    codes: list[str] = []

    def code(match: re.Match) -> str:
        codes.append('<code>' + html.escape(match[1]) + '</code>')
        return f'\x00{len(codes)-1}\x00'

    value = re.sub(r'`([^`]+)`', code, value)
    value = html.escape(value)
    value = re.sub(r'\[([^\]]+)\]\(([^\s)]+)\)',
                   lambda m: '<a href="' + m[2] + '">' + m[1] + '</a>', value)
    value = re.sub(r'\*\*(.+?)\*\*', r'<strong>\1</strong>', value)
    value = re.sub(r'\[(S\d\d)\]', r'<a class="citation" href="#source-\1">[\1]</a>', value)
    for i, item in enumerate(codes):
        value = value.replace(f'\x00{i}\x00', item)
    return value


def markdown(value: str) -> str:
    result: list[str] = []
    list_type: str | None = None
    fence: list[str] | None = None
    for line in value.splitlines():
        if line.startswith('```'):
            if list_type:
                result.append(f'</{list_type}>')
                list_type = None
            if fence is None:
                fence = []
            else:
                result.append('<pre><code>' + html.escape('\n'.join(fence)) + '</code></pre>')
                fence = None
            continue
        if fence is not None:
            fence.append(line)
            continue
        bullet = re.match(r'^\s*(- |\d+\. )(.+)$', line)
        if bullet:
            new_type = 'ul' if bullet[1] == '- ' else 'ol'
            if list_type != new_type:
                if list_type:
                    result.append(f'</{list_type}>')
                result.append(f'<{new_type}>')
                list_type = new_type
            result.append('<li>' + inline(bullet[2]) + '</li>')
            continue
        if list_type:
            result.append(f'</{list_type}>')
            list_type = None
        if not line.strip():
            continue
        heading = re.match(r'^(#{1,6}) (.*)$', line)
        if heading:
            level = min(6, len(heading[1]) + 1)
            result.append(f'<h{level}>' + inline(heading[2]) + f'</h{level}>')
        else:
            result.append('<p>' + inline(line) + '</p>')
    if list_type:
        result.append(f'</{list_type}>')
    if fence is not None:
        raise ValueError('Unclosed Markdown fence')
    return '\n'.join(result)


TEMPLATE = r'''<!doctype html>
<html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<meta name="color-scheme" content="light"><meta name="plan-source-sha256" content="__HASH__">
<title>Booster Pack Heroes · 小游戏发行计划书</title>
<style>
:root{--paper:#f5f3ec;--ink:#222820;--muted:#62695c;--line:#d8dbcf;--accent:#a4401c;--light:#fffef9;--soft:#e7ebdf;--warn:#754219;--ok:#365840}
*{box-sizing:border-box}html{scroll-behavior:smooth;scroll-padding-top:80px}body{margin:0;color:var(--ink);background:var(--paper);font-family:"Microsoft YaHei","PingFang SC",sans-serif;font-size:15px;line-height:1.75}
a{color:var(--accent);text-underline-offset:3px}button,input,select{font:inherit}button,select{cursor:pointer}button{color:var(--ink);background:var(--light);border:1px solid var(--line);border-radius:3px;padding:8px 13px}button:active{transform:translateY(1px)}:focus-visible{outline:3px solid var(--accent);outline-offset:3px}
.sheet{max-width:1320px;margin:auto;padding:0 42px}.masthead{padding:18px 0;border-bottom:2px solid var(--ink);display:flex;justify-content:space-between;gap:20px;font-size:12px;letter-spacing:.08em}.brand{font-weight:700}.edition{color:var(--muted);text-align:right}
.hero{display:grid;grid-template-columns:1.8fr 1fr;gap:50px;padding:52px 0 38px;align-items:end}.eyebrow{font-size:12px;letter-spacing:.16em;color:var(--accent);margin:0 0 15px}.hero h1{font-family:"Noto Serif CJK SC","Songti SC",SimSun,serif;font-size:clamp(32px,4.2vw,58px);font-weight:700;line-height:1.12;margin:0 0 24px;letter-spacing:-.025em}.hero h1 span{display:block;font-size:.55em;margin-top:14px;letter-spacing:.02em}.lede{font-size:16px;max-width:700px;margin:0;color:var(--muted)}.hero-note{border-top:2px solid var(--accent);padding-top:16px;font-size:14px}.hero-note strong{display:block;color:var(--accent);margin-bottom:9px}.tag{font-size:11px;display:inline-block;padding:2px 7px;background:var(--soft);border-radius:2px;color:var(--ok);line-height:1.6}.tag.pending{background:#eee4d4;color:var(--warn)}
.numbers{display:grid;grid-template-columns:repeat(4,1fr);gap:22px;border-top:1px solid var(--line);border-bottom:1px solid var(--line);padding:22px 0}.number{font-size:29px;font-weight:600;line-height:1.2;font-variant-numeric:tabular-nums}.number small{font-size:13px;font-weight:400}.number-label{font-size:12px;color:var(--muted);margin-top:8px}
.nav{position:sticky;top:0;z-index:5;display:flex;flex-wrap:wrap;gap:18px;background:var(--paper);border-bottom:1px solid var(--line);padding:13px 0;font-size:13px}.nav a{color:var(--ink);text-decoration:none}.nav a:hover{color:var(--accent)}.nav .print{margin-left:auto;font-size:12px;padding:1px 9px}
section{padding:39px 0;border-bottom:1px solid var(--line)}.section-head{display:flex;justify-content:space-between;align-items:baseline;gap:18px;margin-bottom:21px}h2{font-family:"Noto Serif CJK SC","Songti SC",SimSun,serif;font-size:27px;line-height:1.3;margin:0}h2 small{font:12px "Microsoft YaHei",sans-serif;color:var(--accent);margin-right:12px}h3{font-size:17px;margin:20px 0 8px}.hint,.caption{font-size:12px;color:var(--muted)}.caption{margin:12px 0 0}.priority{display:grid;grid-template-columns:1fr 1fr;gap:0 36px}.priority article{padding:17px 0;border-top:1px solid var(--line)}.priority h3{margin:0 0 8px}.priority p{font-size:14px;margin:0}.index-no{color:var(--accent);font-size:12px;margin-right:9px}
.timeline-wrap{overflow:auto}.timeline{min-width:700px}.timeline-row{display:grid;grid-template-columns:180px repeat(12,minmax(30px,1fr));gap:3px;margin:6px 0;align-items:center}.timeline-row>span{font-size:12px}.timeline-label{padding-right:12px}.week{color:var(--muted);font-size:11px;text-align:center}.bar{height:25px;background:var(--soft);font-size:10px;line-height:25px;padding-left:9px;white-space:nowrap;overflow:hidden;border-left:3px solid var(--ok)}.bar.follow{background:#eee4d4;border-color:var(--accent)}
.gates{margin-top:27px;display:grid;grid-template-columns:repeat(3,1fr);gap:21px}.gate{border-top:2px solid var(--line);padding-top:12px}.gate h3{font-size:14px;margin:0 0 7px}.gate p{font-size:12px;margin:0;color:var(--muted)}.gate .tag{margin-top:8px}
.controls{display:flex;flex-wrap:wrap;gap:11px;align-items:end;margin:18px 0}.controls label{display:grid;gap:4px;font-size:12px;color:var(--muted)}input,select{border:1px solid #b9c1af;border-radius:3px;background:var(--light);color:var(--ink);padding:7px 9px;max-width:100%;min-width:0}input[type=search]{width:330px}.table-scroll{width:100%;overflow:auto}table{border-collapse:collapse;width:100%;font-size:12px;line-height:1.6}th{text-align:left;font-weight:500;color:var(--muted);background:var(--soft);white-space:nowrap}th,td{padding:10px 12px;border-bottom:1px solid var(--line);vertical-align:top}td.id{color:var(--accent);white-space:nowrap;font-variant-numeric:tabular-nums}.tasks-table{min-width:1050px}.tasks-table td:nth-child(3){min-width:170px}.tasks-table td:nth-child(6){min-width:190px}.tasks-table td:nth-child(7){min-width:280px}.tasks-table td:nth-child(4){min-width:100px}.empty{padding:22px;font-size:13px;color:var(--muted)}
.finance{display:grid;grid-template-columns:1.15fr .85fr;gap:35px}.panel{background:var(--light);border:1px solid var(--line);padding:22px}.panel h3{margin-top:0}.input-grid{display:grid;grid-template-columns:repeat(3,1fr);gap:12px}.input-grid label{font-size:11px;color:var(--muted);display:grid;gap:5px}.input-grid input{width:100%;font-size:14px;font-variant-numeric:tabular-nums}.result-grid{display:grid;grid-template-columns:1fr 1fr;gap:20px;border-top:1px solid var(--line);padding-top:19px;margin-top:19px}.result-grid .value{font-size:23px;font-variant-numeric:tabular-nums}.result-grid .label{font-size:11px;color:var(--muted)}.value.negative{color:var(--accent)}.formula{font-family:Consolas,monospace;font-size:12px;background:var(--soft);padding:12px;margin:16px 0 0;overflow-wrap:anywhere}.budget-total{font-size:32px;font-weight:600;margin:16px 0 4px;line-height:1.3}.notice{padding:14px 18px;background:#eee4d4;font-size:12px;margin:18px 0 0}.economics-table{min-width:640px}
.split{display:grid;grid-template-columns:1.2fr .8fr;gap:35px}.flow{display:flex;flex-wrap:wrap;gap:7px;align-items:center;margin:16px 0}.flow span{padding:4px 8px;background:var(--soft);font-size:12px}.flow b{font-size:11px;color:var(--muted)}.rule-list{padding-left:20px;font-size:14px}.rule-list li{margin-bottom:7px}.qa-list{display:grid;grid-template-columns:1fr 1fr;gap:0 24px;margin-top:20px}.qa-item{padding:12px 0;border-top:1px solid var(--line);font-size:12px}.qa-item strong{display:block;margin-bottom:4px}.qa-item p{margin:0;color:var(--muted)}details{margin-top:16px}summary{cursor:pointer;color:var(--accent);font-size:13px;padding:10px 0}.risk{font-size:13px;border-top:1px solid var(--line);padding:12px 0}.risk p{margin:6px 0;color:var(--muted);font-size:12px}
.prose{max-width:900px}.prose h2{font-size:24px;margin:24px 0 20px}.prose h3{font-family:"Songti SC",SimSun,serif;font-size:23px;margin-top:38px;padding-bottom:10px;border-bottom:1px solid var(--line)}.prose h4{font-size:17px;margin-top:26px}.prose p,.prose li{font-size:14px;line-height:1.95}.prose li{margin:7px 0}.prose code{font-family:Consolas,monospace;font-size:.9em;padding:2px 4px;background:var(--soft);overflow-wrap:anywhere}.prose a{overflow-wrap:anywhere}.prose pre{white-space:pre-wrap;overflow-wrap:anywhere}.citation{font-size:11px;text-decoration:none}.source-table{min-width:840px}.source-table td:nth-child(2){min-width:220px}.source-table td:nth-child(4){min-width:290px}.source-table tr:target{background:#eee4d4}.footer{padding:26px 0 40px;display:flex;justify-content:space-between;gap:20px;font-size:11px;color:var(--muted)}
@media(max-width:900px){.sheet{padding:0 24px}.hero{grid-template-columns:1fr;gap:26px;padding-top:35px}.finance,.split{grid-template-columns:1fr}.hero-note{max-width:650px}.gates{grid-template-columns:1fr 1fr}.section-head{display:block}.section-head .hint{margin-top:10px}.input-grid{grid-template-columns:repeat(3,1fr)}}
@media(max-width:560px){.sheet{padding:0 16px}.masthead{font-size:10px;gap:10px}.edition{max-width:150px}.hero{padding-top:29px}.hero h1{font-size:35px}.hero h1 span{font-size:clamp(15px,4.3vw,20px);letter-spacing:0}.numbers{grid-template-columns:1fr 1fr;gap:23px}.number{font-size:26px}.nav{gap:8px 16px;font-size:12px;padding:10px 0}.nav .print{margin-left:0}section{padding:29px 0}h2{font-size:24px}.priority,.gates,.qa-list{grid-template-columns:1fr}.priority{gap:0}.controls{align-items:stretch}.controls label{width:100%}input[type=search]{width:100%}.input-grid{grid-template-columns:1fr 1fr}.panel{padding:16px}.result-grid .value{font-size:21px}.footer{display:block}.lede{font-size:14px}.hero-note{font-size:13px}}
@media(prefers-reduced-motion:reduce){html{scroll-behavior:auto}button:active{transform:none}}
@media print{body{background:white;font-size:10px}.sheet{max-width:none;padding:0}.nav,.controls,.print{display:none!important}.hero{padding:16px 0;gap:20px;grid-template-columns:1.6fr 1fr}.hero h1{font-size:29px}.lede,.hero-note{font-size:10px}.numbers{padding:12px 0}.number{font-size:19px}section{padding:18px 0}h2{font-size:21px}.section-head{break-after:avoid}.finance,.split{display:block}.panel{margin-bottom:15px}.table-scroll{overflow:visible}.tasks-table,.source-table,.economics-table{min-width:0;width:100%;table-layout:fixed}table{font-size:8px}td,th{padding:5px;white-space:normal!important;overflow-wrap:anywhere;min-width:0!important}tr{break-inside:avoid}.prose{max-width:none}.prose p,.prose li{font-size:10px}.prose h3{font-size:19px;margin-top:20px}.prose h4{font-size:14px}.qa-list{grid-template-columns:1fr 1fr}.hint,.caption{font-size:9px}.footer{font-size:8px}.timeline-row{grid-template-columns:150px repeat(12,minmax(0,1fr))}.timeline{min-width:0}.timeline-wrap{overflow:visible}a{color:inherit}}
</style></head><body><div class="sheet">
<header class="masthead"><div class="brand">ROGUE PROTOTYPE LAB / 发行工作簿</div><div class="edition">2026.11.18 · V1.0<br>中国大陆 · 授权移植路线</div></header>
<div class="hero"><div><p class="eyebrow">RELEASE PLANNING / BOOSTER PACK HEROES</p><h1>从开包体验，<br>到小游戏发行。<span>移植 · 广告 · 推广 · 持续运营</span></h1><p class="lede">微信小游戏先行，抖音随后验证。把可玩版本、广告奖励、准入材料和现金回收串成一条可检查的执行路线。</p></div><aside class="hero-note"><strong>当前交付：研究与计划</strong>用户已确认取得商业改编授权，范围后续补充。全部生产检查点尚未验收；预算、性能和收益场景均为建议或假设。<br><br><span class="tag">微信先行 / 用户确认</span> <span class="tag pending">广告与上线 / 待执行</span></aside></div>
<div class="numbers"><div><div class="number" id="taskCount"></div><div class="number-label">任务 · 每项有依赖、产物与验收</div></div><div><div class="number" id="pdCount"></div><div class="number-label">人日估算 · 不是实际已投入工时</div></div><div><div class="number">12 <small>周工作窗口</small></div><div class="number-label">需约2–3等效角色容量；外部等待另列</div></div><div><div class="number">2.2–11.33 <small>万元</small></div><div class="number-label">现金预算建议 · 不含授权保底等费用</div></div></div>
<nav class="nav" aria-label="计划导航"><a href="#decisions">先解的四件事</a><a href="#roadmap">阶段与检查点</a><a href="#tasks">任务清单</a><a href="#finance">预算与收益</a><a href="#adops">广告与验收</a><a href="#full-plan">完整计划</a><a href="#sources">来源</a><button class="print" id="printButton">打印 / 保存</button></nav>
<main>
<section id="decisions"><div class="section-head"><h2><small>01</small>先解的四件事</h2><span class="hint">先通过必要检查，再扩大制作与投入</span></div><div class="priority">
<article><h3><span class="index-no">A</span>核对卡包规则</h3><p>金币买随机包、广告间接随机抽取都需按平台条款核查。提审候选采用固定或自选奖励，保留开包表现。</p></article>
<article><h3><span class="index-no">B</span>确认真实准入路径</h3><p>授权范围留档；核对主体、作品属性和支付策略。“首版不内购”不等于可以替用户承诺未来永不付费。</p></article>
<article><h3><span class="index-no">C</span>取得可玩真机证据</h3><p>现有编译与托管检查不代替完整核心循环。先验证微信输入、保存、移动渲染与真实广告奖励。</p></article>
<article><h3><span class="index-no">D</span>按现金判断推广</h3><p>分别核算可结算现金、广告金和合同分成。先小流量学习，完整留存与真实回收支持后再扩大买量。</p></article></div></section>
<section id="roadmap"><div class="section-head"><h2><small>02</small>阶段与检查点</h2><span class="hint">相对T0安排；目前未启动生产检查点</span></div><div class="timeline-wrap"><div class="timeline" id="timeline"></div></div><p class="caption">工作窗口：12周。完整D30/90观察和抖音正式验证可能延伸；若一位协调者串行承担，建议预留30–36周。审核等待不计连续人日，不能保证在窗口内完成。</p><div class="gates" id="gates"></div></section>
<section id="tasks"><div class="section-head"><h2><small>03</small>可执行任务清单</h2><span class="hint" id="taskShown" aria-live="polite"></span></div><p class="hint">角色表示责任能力，由助手承担准备与制作；身份、签署、付款和审批由有权主体完成。这里所有任务均为计划，不是已经完成的游戏功能。</p><div class="controls"><label>查找任务<input id="taskSearch" type="search" placeholder="例如：奖励、备案、归因"></label><label>阶段<select id="phaseFilter"><option value="">全部阶段</option></select></label><label>责任角色<select id="roleFilter"><option value="">全部角色</option></select></label><button id="clearFilter">清除筛选</button></div><div class="table-scroll"><table class="tasks-table"><thead><tr><th>ID / 优先级</th><th>阶段</th><th>任务</th><th>责任角色 / 人日</th><th>依赖</th><th>交付物</th><th>通过标准 / 外部条件</th></tr></thead><tbody id="taskRows"></tbody></table></div><div id="emptyTasks" class="empty" hidden>没有符合筛选条件的任务。</div></section>
<section id="finance"><div class="section-head"><h2><small>04</small>预算与假设收益</h2><span class="hint">全部为规划假设，无本品实测收入或供应商报价</span></div><div class="finance"><div class="panel"><h3>广告现金贡献计算器</h3><p class="hint">eCPM采用平台分成后、可结算的现金口径。若输入的是平台前流水，须先换算；不把广告金混入。</p><div class="controls"><label>假设场景<select id="scenarioSelect"></select></label><button id="resetModel">恢复此场景</button></div><div class="input-grid" id="modelInputs"></div><div class="result-grid" id="modelOutputs" aria-live="polite"></div><div class="formula">展示/DAU = 机会 × 自选率 × 填充率 × 展示率<br>LTV90 = [现金ARPDAU × (1−追加分成) − 日服务成本] × 活跃天数</div><p class="caption">完成率用于奖励/体验判断，不在按展示计价的收入上重复相乘。贡献不含固定研发、税费与资金成本；模型结果不能作为买量报价。</p></div><div><div class="controls"><label>预算方案<select id="budgetSelect"><option value="lean">精简验证</option><option value="base" selected>基准方案</option><option value="expanded">扩展验证</option></select></label></div><div class="budget-total" id="budgetTotal" aria-live="polite"></div><p class="hint">人民币，含10%预备金；尚未批准支出。</p><div class="table-scroll"><table><thead><tr><th>现金预留项目</th><th>金额 / 元</th></tr></thead><tbody id="budgetRows"></tbody></table></div><div class="notice">不含版权保底/预付款、可能适用的出版申报、公司设立、薪酬和税费。首轮微信实验建议现金≤5,000元、日≤500元；预算通过后才执行。</div></div></div><h3>三个场景的初始值对照</h3><div class="table-scroll"><table class="economics-table"><thead><tr><th>场景（假设）</th><th>展示 / DAU</th><th>净现金eCPM / 元</th><th>现金ARPDAU / 元</th><th>90日活跃天数</th><th>贡献LTV90 / 元</th></tr></thead><tbody id="scenarioRows"></tbody></table></div><p class="caption">放量建议：成熟D30已实现贡献回收≥70%，且保守LTV90 / 实际CPA≥1.3，并满足体验与现金安全条件。未成熟cohort仅允许在有限实验预算内学习。</p></section>
<section id="adops"><div class="section-head"><h2><small>05</small>广告闭环与质量</h2><span class="hint">先完成两类自愿激励位；其它广告形态后续受控验证</span></div><div class="split"><div><h3>一次自愿观看，对应一次确定权益</h3><div class="flow"><span>主动点击</span><b>→</b><span>冻结奖励/事务</span><b>→</b><span>加载/展示</span><b>→</b><span>明确完成</span><b>→</b><span>去重提交</span><b>→</b><span>保存/反馈</span></div><ul class="rule-list"><li>正常结算先保存，再提供可跳过的固定额外资源或明确补给。</li><li>取消、失败、未知结果不猜测为完成；后台返回不能作为观看凭据。</li><li>服务端票据和去重不等于观看鉴真；是否有官方服务端验证须补查。</li><li>建议教程/前三分钟不提示；120秒间隔、日6次完成奖励为实验初值。</li><li>无库存、拒绝非必要授权或关闭广告后，正常主循环仍可继续。</li></ul></div><aside><h3>广告资格不等于已开通</h3><p class="hint">官方有UV大于500、有效游戏圈与5篇官方帖、同主体既有变现小程序等不同路径。不得刷用户或点击自家正式广告。</p><p class="hint">若平台需要先发布形成访问，单列合规验证发布方案；本轮不执行发布。严格要求任何公开访问前真广告时，条件未满足就继续内部验证。</p></aside></div><details><summary>展开24类验收场景 · 当前均未运行</summary><div class="qa-list" id="qaList"></div></details><details><summary>展开风险与处置责任</summary><div id="riskList"></div></details></section>
<section id="full-plan"><div class="section-head"><h2><small>06</small>完整计划书</h2><span class="hint"><a href="PLAN.md">Markdown正文</a> · <a href="plan-data.json">结构化数据</a> · <a href="WORKBREAKDOWN.md">任务清单</a></span></div><article class="prose">__PROSE__</article></section>
<section id="sources"><div class="section-head"><h2><small>07</small>来源与覆盖边界</h2><span class="hint" id="sourceCount"></span></div><p class="hint">本次18个官方平台入口中，部分仅有短正文或导航；另2项监管背景仅定位原文。未登录账号核验实际资格，未获得市场收入与买量报价。页面编辑日期、施行日期和观察日期分别记录。</p><div class="controls"><label>阅读范围<select id="sourceFilter"><option value="">全部来源</option><option value="full">正文提取完整</option><option value="text">公开正文/有限正文</option><option value="gap">仅导航/摘要，需补查</option></select></label></div><div class="table-scroll"><table class="source-table"><thead><tr><th>ID</th><th>来源与日期</th><th>阅读级别</th><th>采用范围与缺口</th></tr></thead><tbody id="sourceRows"></tbody></table></div><details><summary>调研工具与无法确认的事项</summary><ul class="rule-list" id="researchLimits"></ul></details><p class="caption">来源观察日2026-11-18；过去30天窗口2026-10-20至11-18。法规与平台适用仍需有权主体确认，本计划不构成法律意见。<a href="../../../sources/references/booster-pack-heroes-miniapp-2026-11-18/README.md">详细来源登记</a></p></section>
</main><footer class="footer"><div>Rogue Prototype Lab · BPH mini-game release planning<br>研究与规划资料 / 游戏和平台验证另行留证</div><div>离线单文件 · 数据源 plan-data.json / 正文 PLAN.md<br>更新后运行 render_plan.py 重建</div></footer></div>
<script id="planData" type="application/json">__DATA__</script>
<script>
'use strict';
const D=JSON.parse(document.getElementById('planData').textContent);
const $=id=>document.getElementById(id);
const esc=v=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const money=v=>new Intl.NumberFormat('zh-CN',{maximumFractionDigits:0}).format(v);
const num=(v,n=4)=>Number(v).toFixed(n);
$('taskCount').innerHTML=D.tasks.length+' <small>项任务</small>';
$('pdCount').innerHTML=D.staffing.estimatedTotalPersonDays+' <small>人日</small>';
const phaseName=id=>D.phases.find(p=>p.id===id)?.title||id;
$('timeline').innerHTML='<div class="timeline-row"><span class="timeline-label">相对执行启动日 T0</span>'+Array.from({length:12},(_,i)=>'<div class="week">W'+(i+1)+'</div>').join('')+'</div>'+D.phases.map(p=>'<div class="timeline-row"><span class="timeline-label">'+esc(p.id+' / '+p.title)+'</span><div class="bar '+(p.id==='G'?'follow':'')+'" style="grid-column:'+(p.startWeek+1)+' / '+(p.endWeek+2)+'">'+esc('W'+p.startWeek+'–'+p.endWeek+(p.extendsBeyondWindow?' · 后续可延长':''))+'</div></div>').join('');
$('gates').innerHTML=D.gates.map(g=>'<article class="gate"><h3>'+esc(g.id+' / '+g.title)+'</h3><p>'+esc(g.criteria)+'</p><span class="tag pending">未评估</span></article>').join('');
$('phaseFilter').innerHTML+=D.phases.map(p=>'<option value="'+p.id+'">'+esc(p.id+' '+p.title)+'</option>').join('');
$('roleFilter').innerHTML+=Object.entries(D.roles).map(([id,name])=>'<option value="'+id+'">'+esc(name)+'</option>').join('');
function renderTasks(){const q=$('taskSearch').value.trim().toLowerCase(),phase=$('phaseFilter').value,role=$('roleFilter').value;const rows=D.tasks.filter(t=>(!phase||t.phase===phase)&&(!role||t.owner===role)&&(!q||[t.id,t.title,t.deliverable,t.acceptance,t.externalDependency||''].join(' ').toLowerCase().includes(q)));$('taskShown').textContent=rows.length+' / '+D.tasks.length+'项 · '+rows.reduce((a,t)=>a+t.estimatedPersonDays,0)+'计划人日';$('taskRows').innerHTML=rows.map(t=>'<tr><td class="id">'+esc(t.id)+'<br>'+esc(t.priority)+'</td><td>'+esc(phaseName(t.phase))+'</td><td><strong>'+esc(t.title)+'</strong><br><span class="hint">计划</span></td><td>'+esc(D.roles[t.owner])+'<br>'+t.estimatedPersonDays+'人日</td><td>'+esc(t.dependsOn.join(', ')||'起点')+'</td><td>'+esc(t.deliverable)+'</td><td>'+esc(t.acceptance)+(t.externalDependency?'<br><span class="hint">外部：'+esc(t.externalDependency)+'</span>':'')+'</td></tr>').join('');$('emptyTasks').hidden=rows.length>0;}
['taskSearch','phaseFilter','roleFilter'].forEach(id=>$(id).addEventListener('input',renderTasks));
$('clearFilter').addEventListener('click',()=>{['taskSearch','phaseFilter','roleFilter'].forEach(id=>$(id).value='');renderTasks();});renderTasks();
const fields=[['opportunities','每日可选机会 / 次',0,50,1],['optIn','主动选择率 / 0–1',0,1,.01],['fill','填充率 / 0–1',0,1,.01],['show','展示成功率 / 0–1',0,1,.01],['netEcpm','净现金eCPM / 元',0,10000,1],['extraShare','合同追加分成 / 0–1',0,1,.01],['costPerActiveDay','每活跃日服务成本 / 元',0,100,.001],['activeDays90','90日内活跃天数 / 天',0,90,1]];
$('scenarioSelect').innerHTML=D.economics.scenarios.map(s=>'<option value="'+s.id+'" '+(s.id==='base'?'selected':'')+'>'+esc(s.name)+'</option>').join('');
$('modelInputs').innerHTML=fields.map(([id,label,min,max,step])=>'<label for="model-'+id+'">'+esc(label)+'<input id="model-'+id+'" type="number" min="'+min+'" max="'+max+'" step="'+step+'" inputmode="decimal"></label>').join('');
function model(){let v={};for(const [id,label,min,max] of fields){const input=$('model-'+id),value=Number(input.value);v[id]=Number.isFinite(value)?Math.min(max,Math.max(min,value)):min;}const im=v.opportunities*v.optIn*v.fill*v.show,arp=im*v.netEcpm/1000,ltv=(arp*(1-v.extraShare)-v.costPerActiveDay)*v.activeDays90;const cpa=Math.max(0,ltv/1.3);$('modelOutputs').innerHTML=[['展示 / DAU / 日',num(im,3)],['现金ARPDAU / 元',num(arp)],['贡献LTV90 / 元',num(ltv)],['LTV/CPA=1.3时的假设CPA / 元',num(cpa)]].map(([label,value])=>'<div><div class="value '+(Number(value)<0?'negative':'')+'">'+value+'</div><div class="label">'+esc(label)+'</div></div>').join('');}
function resetModel(){const s=D.economics.scenarios.find(x=>x.id===$('scenarioSelect').value);fields.forEach(([id])=>$('model-'+id).value=s[id]);model();}
$('scenarioSelect').addEventListener('change',resetModel);$('resetModel').addEventListener('click',resetModel);fields.forEach(([id])=>$('model-'+id).addEventListener('input',model));resetModel();
function renderBudget(){const k=$('budgetSelect').value,b=D.budget.scenarios[k];$('budgetTotal').textContent='¥ '+money(b.total);$('budgetRows').innerHTML=D.budget.items.map(x=>'<tr><td>'+esc(x.name)+'</td><td>'+money(x[k])+'</td></tr>').join('')+'<tr><td>预备金 / 10%</td><td>'+money(b.reserve)+'</td></tr>';}
$('budgetSelect').addEventListener('change',renderBudget);renderBudget();
$('scenarioRows').innerHTML=D.economics.scenarios.map(s=>'<tr><td>'+esc(s.name)+'</td><td>'+num(s.impressionsPerDAU,3)+'</td><td>'+s.netEcpm+'</td><td>'+num(s.cashARPDAU)+'</td><td>'+s.activeDays90+'</td><td>'+num(s.contributionLTV90)+'</td></tr>').join('');
$('qaList').innerHTML=D.qaCases.map(c=>'<div class="qa-item"><strong>'+esc(c.id+' / '+c.scenario)+'</strong><p>'+esc(c.expected)+' · 未运行</p></div>').join('');
$('riskList').innerHTML=D.risks.map(r=>'<article class="risk"><strong>'+esc(r.id+' / '+r.priority+' / '+r.risk)+'</strong><p>'+esc(r.mitigation)+'</p><span class="hint">责任：'+esc(D.roles[r.owner])+' · '+esc(r.tasks.join(', '))+'</span></article>').join('');
const levels={full:'完整正文',text:'公开正文',limited_text:'有限正文',limited_images:'正文+未目检图片',navigation_only:'仅导航 / 待补读',search_snippet_only:'仅搜索摘要',local_documents:'本地记录快照'};
function renderSources(){const filter=$('sourceFilter').value;const rows=D.researchSources.filter(s=>!filter||(filter==='full'&&s.readLevel==='full')||(filter==='text'&&['text','limited_text','limited_images','local_documents'].includes(s.readLevel))||(filter==='gap'&&['navigation_only','search_snippet_only'].includes(s.readLevel)));$('sourceCount').textContent=rows.length+' / '+D.researchSources.length+'项来源';$('sourceRows').innerHTML=rows.map(s=>'<tr id="source-'+s.id+'"><td class="id">'+s.id+'</td><td>'+(s.url?'<a target="_blank" rel="noopener noreferrer" href="'+esc(s.url)+'">'+esc(s.title)+'</a>':esc(s.title))+'<br><span class="hint">观察：'+s.observedAt+(s.lastEditedAt?'<br>编辑/日志：'+s.lastEditedAt:'')+(s.effectiveAt?'<br>执行说明：'+s.effectiveAt:'')+'</span></td><td>'+esc(levels[s.readLevel]||s.readLevel)+'</td><td>'+esc(s.summary)+'</td></tr>').join('');}
$('sourceFilter').addEventListener('change',renderSources);renderSources();
document.addEventListener('click',event=>{const link=event.target.closest('a[href^="#source-"]');if(link&&$('sourceFilter').value){$('sourceFilter').value='';renderSources();}});
$('researchLimits').innerHTML=D.researchLimits.map(x=>'<li>'+esc(x)+'</li>').join('');
$('printButton').addEventListener('click',()=>window.print());
window.addEventListener('beforeprint',()=>{document.querySelectorAll('details').forEach(x=>{x.dataset.printWasOpen=x.open?'1':'0';x.open=true;});});
window.addEventListener('afterprint',()=>{document.querySelectorAll('details').forEach(x=>{x.open=x.dataset.printWasOpen==='1';});});
</script></body></html>'''


def render() -> None:
    plan = (ROOT / 'PLAN.md').read_text(encoding='utf-8')
    data_text = (ROOT / 'plan-data.json').read_text(encoding='utf-8')
    data = json.loads(data_text)
    embedded = json.dumps(data, ensure_ascii=False).replace('<', '\\u003c')
    digest = hashlib.sha256((plan + '\n' + data_text).encode('utf-8')).hexdigest()
    output = TEMPLATE.replace('__HASH__', digest).replace('__PROSE__', markdown(plan)).replace('__DATA__', embedded)
    (ROOT / 'index.html').write_bytes((output + '\n').encode('utf-8'))
    lines = ['# Booster Pack Heroes 小游戏执行任务清单', '',
             f"2026-11-18 v1.0；{len(data['tasks'])} 项，{data['staffing']['estimatedTotalPersonDays']} 计划人日。由 [plan-data.json](plan-data.json) 生成，修改数据后运行 render_plan.py。", '',
             '所有任务均为计划；人日不含审核等外部等待。角色表示职责而非现有团队人数；身份、签署、付款和平台审批由有权主体完成，助手准备材料。', '']
    for phase in data['phases']:
        lines += [f"## {phase['id']} / {phase['title']}", '']
        for task in [t for t in data['tasks'] if t['phase'] == phase['id']]:
            lines += [f"### {task['id']} {task['title']}", '',
                      f"- 责任：{data['roles'][task['owner']]}；估算 {task['estimatedPersonDays']} 人日；{task['priority']}；状态：planned。",
                      f"- 依赖：{', '.join(task['dependsOn']) or '起点'}。",
                      f"- 交付物：{task['deliverable']}。",
                      f"- 验收：{task['acceptance']}。",
                      f"- 拟保存证据：`{task['evidence']}`。"]
            if task['externalDependency']:
                lines.append(f"- 外部条件：{task['externalDependency']}。")
            lines.append('')
    (ROOT / 'WORKBREAKDOWN.md').write_bytes(('\n'.join(lines).rstrip() + '\n').encode('utf-8'))
    print(json.dumps({'html': 'index.html', 'tasks': len(data['tasks']), 'personDays': data['staffing']['estimatedTotalPersonDays'], 'sourceDigest': digest, 'htmlBytes': len(output.encode('utf-8'))}, ensure_ascii=False))


if __name__ == '__main__':
    render()
