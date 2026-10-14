"""Render Preview-only SteamPipe VDF after release evidence passes. No login/upload."""
from pathlib import Path
import json
from audit_steam_release import ROOT,LOCAL,read,evaluate

def render(template,values):
    text=template
    for key,value in values.items():
        value=str(value).replace('\\','/')
        if any(c in value for c in ('"','\n','\r','\0')):raise ValueError('Unsafe VDF value: '+key)
        text=text.replace('@'+key+'@',value)
    if '@' in text:raise ValueError('Unresolved VDF placeholder')
    return text
def main():
    product=read(ROOT/'release/product.json');rights=read(ROOT/'release/rights-register.json')
    private=LOCAL/'steam-release/private-evidence.json';evidence=read(private) if private.exists() else {}
    audit=evaluate(product,rights,evidence)
    if not audit['readyForBuildReview']:raise SystemExit('Preview configuration stopped: '+', '.join(audit['failedGates']))
    target=LOCAL/'steam-release/steampipe-preview';target.mkdir(parents=True,exist_ok=True)
    values=dict(APP_ID=product['appId'],WINDOWS_DEPOT_ID=product['windowsDepotId'],CONTENT_ROOT=product['candidateBuild'],BUILD_OUTPUT=str(target/'cache'),BUILD_DESCRIPTION='Night-market verified candidate '+product['candidateSourceSHA256'][:12])
    for stem in ('app_build','depot_windows'):
        output=target/(stem+'.vdf')
        if output.exists():raise FileExistsError('Preserve existing generated VDF: '+str(output))
        output.write_text(render((ROOT/'release/steam'/(stem+'.vdf.template')).read_text('utf-8'),values),encoding='utf-8',newline='\n')
    print(json.dumps(dict(previewOnly=True,uploadsExecuted=False,setLive=False,path=str(target)),ensure_ascii=False))
if __name__=='__main__':main()
