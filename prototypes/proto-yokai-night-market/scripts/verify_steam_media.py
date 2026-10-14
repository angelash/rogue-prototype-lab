"""Verify exact Steam candidates and preserve source/prompt/font provenance."""
from pathlib import Path
import hashlib,json
from PIL import Image

ROOT=Path(__file__).resolve().parents[1];MEDIA=ROOT/'release/media'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
    spec=json.loads((MEDIA/'specs.json').read_text('utf-8'));items=[]
    for entry in spec['assets']:
        p=MEDIA/'generated'/(entry['id']+'.'+entry['format']);im=Image.open(p)
        assert im.size==(entry['width'],entry['height']),(p,im.size)
        assert im.format==('JPEG' if entry['format']=='jpg' else 'PNG')
        if entry['id']=='library-logo':
            alpha=im.convert('RGBA').getchannel('A');assert alpha.getextrema()==(0,255)
            x0,y0,x1,y1=alpha.getbbox();assert 0<x0<x1<im.width and 0<y0<y1<im.height
        items.append(dict(id=entry['id'],path=p.relative_to(ROOT).as_posix(),width=im.width,height=im.height,format=im.format,bytes=p.stat().st_size,sha256=sha(p),status='candidate_dimension_verified_human_reviewed_not_published'))
    reference=ROOT/'fidelity/art/generated/SNDGuy.raw.png';sources=[]
    for stem in ('landscape-master-v1','portrait-master-v1'):
        p=MEDIA/'source'/(stem+'.raw.png');prompt=MEDIA/'source'/(stem+'.prompt.txt');im=Image.open(p)
        sources.append(dict(id=stem,path=p.relative_to(ROOT).as_posix(),actualSize=list(im.size),sha256=sha(p),prompt=prompt.relative_to(ROOT).as_posix(),promptSHA256=sha(prompt),tool='image_gen.imagegen built-in',references=[dict(path=reference.relative_to(ROOT).as_posix(),sha256=sha(reference),role='own_original_generated_style_and_character_reference')]))
    font=ROOT/'local/UnityProject/Assets/Recovery/NightFonts/sharedassets0.assets_217.otf'
    notice=ROOT/'local/UnityProject/Assets/StreamingAssets/ThirdPartyNotices/NotoSansCJK-OFL-1.1.txt'
    out=dict(date='2026-10-14',status='draft_candidates_not_approved_or_uploaded',assets=items,sources=sources,technicalExport=dict(script='scripts/export_steam_media.ps1',sampling='single_uniform_scale_centered_cover_crop',text='native Noto glyphs, not imagegen text',fontSHA256=sha(font),fontNoticeSHA256=sha(notice),hero='no text',logo='transparent',note='Generated landscape is 2172x724; hero is technically upsampled to official3840x1240, not claimed as native4K detail. Portrait actual size recorded above.'),scope='Marketing art only. No game texture, stencil, prefab or Player modified; no gameplay screenshot/trailer produced.')
    (MEDIA/'production-manifest.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    print(json.dumps(dict(passed=True,assets=len(items),sources=len(sources),published=False),ensure_ascii=False))
if __name__=='__main__':main()
