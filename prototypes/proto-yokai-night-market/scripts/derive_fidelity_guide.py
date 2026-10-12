"""Derive the current content reference from the authoritative night-market data."""
from pathlib import Path
import json
ROOT=Path(__file__).resolve().parents[1]
def main():
    theme=json.loads((ROOT/'fidelity/data/theme.json').read_text('utf-8'));text=theme['strings'];lines=['# 夜市 v2 全部内容查阅','','由 scripts/derive_fidelity_guide.py 从 theme.json 派生。该 JSON 为替换数据权威；运行行为由本机恢复业务代码决定。旧 v1 设施与章节不在本版。','','## 95 卡','']
    for c in theme['cards']:
        lines += [f"### {c['id']:03d} · {c['name']}",'',c['description'],'',f"Type={c['type']}；Turns={c['turns']}；Target={c['target']}；Function={c['function']}；Num={c['amount']}；Repair={c['repair'] or '空'}；共享图={c['picture'] or 'Default'}。",'',text['Description'+str(c['id'])]['en'],'',f"台词：{text['Quote'+str(c['id'])]['zh']}",'']
    lines += ['## 28 委托','','目标顺序：招徕、料理、备货、灯饰、护阵。源 Animation 不参与 Loader，护阵有效目标为 2。','']
    for p in theme['projects']:
        goals=p['effectiveGoals'];ordered=[goals[k] for k in ['Sound','Programming','Design','Art','Testing']]
        lines += [f"### {p['id']:03d} · {text['Title'+str(p['id'])]['zh']}",'',text['SubTitle'+str(p['id'])]['zh'],'',f"有效目标：{', '.join(map(str,ordered))}；原 Animation={p['ignoredAnimation']}（忽略）。",'']
    lines += ['## 静态示例与共用键显示','','六个教程示例姓名按明确 TextMesh ID 替换，不参与运行卡牌规则。声音开关保留 Sound 标识与保存字段，只给显示文字“声音”/Sound，工作栏的同名键仍显示“招徕”。','']
    for k,v in theme.get('staticTextOverrides',{}).items():lines += [f'- {k}：{v}']
    lines += ['','## 全部 420 文本键','','空键和模板键也保留。语言 0=中文（XML ENG），1=English（XML SPA），不改变原语言槽数量。','']
    for k,v in text.items():lines += [f"### 键 {k or '〈空〉'}",'',v['zh'] or '〈空〉','',v['en'] or '〈空〉','']
    (ROOT/'fidelity/data/content-guide.md').write_text('\n'.join(lines),encoding='utf-8',newline='\n')
    print(json.dumps({'cards':len(theme['cards']),'projects':len(theme['projects']),'texts':len(text)}))
if __name__=='__main__':main()
