"""Original art roles. Keep mechanical geometry and runtime dimensions in the local export."""
import json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]

def role(n):
    if n.endswith('_Mask'):
        return 'A completely plain neutral white silhouette of a rounded rectangular card, flat unlit white fill. It is a tint and fade mask: no ornament, no texture, no text, no frame, no shadow.'
    if n in ['Solid','PRJ_BUTTON','CARD_04_Turn']:
        return 'A completely uniform flat pure white square, fully filled edge to edge, no border or decoration. A tiny functional color-tint texture; no texture, no illumination gradient.'
    if n.startswith('CARD_01_Base_') or n=='PRJ_CARD_01_Base_Project':
        return 'A flat rounded rectangular dark indigo card plate, aspect 1240:924. Extremely quiet charcoal-indigo center for white native text. Narrow amber timber rim and tiny corner knots only within outer 3 percent. No symbols or small panels in the center. Restrained paper grain, high contrast text support.'
    if n in ['CARD_01_BaseTop','CARD_00_Background','CARD_03_DetailsTop']:
        return 'A flat dark indigo rounded card header and picture frame, 1240:831 landscape silhouette. Outer rim 2 percent. Upper header band 20 percent tall remains dark and blank for white native title text. Large empty picture aperture occupies x=5..95 percent, y=22..94 percent. Slim muted amber wooden edging. No objects inside the aperture.'
    if n in ['CARD_02_Details','PRJ_CARD_02_Details']:
        return 'A very quiet flat dark indigo translucent-paper information panel, nearly rectangular with mildly rounded corners, narrow muted amber rim. Center uniformly dark for many white native numbers and labels, no internal dividers or symbols. No text.'
    if n=='CARD_Score':
        return 'A wide flat dark indigo results ledger panel with restrained amber wood rim and a tiny paper lantern knot at each outer corner. Blank evenly dark interior, room for a ten-row white native-text leaderboard, no ruled lines or writing.'
    if n in ['CARD_Selection','CARD_Selection_Background']:
        return 'A flat rounded small dark indigo selection tile, aspect 310:272. Blank center for a white native department symbol and number. Thin warm amber outline, subtle brighter inner glow only for selected state. No icon in the center.'
    if n.startswith('CARD_10_Light'):
        return 'A very wide and thin warm amber selection glow strip, near horizontal rectangular shape. Soft pale-gold glow in middle fading toward long edges. No object, no text. This is an overlay highlight, not a card.'
    if n.startswith('TitleBackground Line'):
        return 'A long thin muted honey-gold and indigo edge strip for a night-market title menu. Mostly transparent, faint single woodcut ornament at the far outer end, clean continuous horizontal line. No text or icon in center.'
    if n.startswith('TitleBackground'):
        return 'A very wide shallow flat dark indigo menu ribbon, 1920:280. Uniform quiet dark center for white native option text. Subtle amber timber edge and tiny lantern knot only at far edge. No text, no large decorative object.'
    if n.startswith('Button') or n=='PRJ_Button_Gradient':
        return 'A flat simple blank dark indigo wooden button face, soft rounded corners, narrow amber outer rim. Calm evenly dark center for white native text. No emblem, no text, no dimensional mockup, no drop shadow.'
    if n=='SubTitle':
        return 'A small horizontal original night-market lantern knot emblem for a card back, sage and muted amber, very simple shapes, transparent surrounding. Central two small paper lanterns on a single string. No writing or wordmark.'
    if n in ['English','Spanish']:
        return ('A small flat circular language badge with a stylized red lantern on cream inside a narrow amber rim, transparent exterior. Clean simple silhouette, no writing, no flags.' if n=='English' else 'A small flat circular language badge with a stylized blue paper fan on cream inside a narrow amber rim, transparent exterior. Clean simple silhouette, no writing, no flags.')
    if n=='FlagHighlight': return 'A thin bright muted-amber circular highlight ring, transparent interior and exterior, no fill and no text. The ring is centered and nearly fills its square.'
    if n in ['OptionRed','OptionGreen','OptionLight']:
        return 'A small flat circular indicator lamp, '+({'OptionRed':'muted terracotta red','OptionGreen':'sage green','OptionLight':'pale amber white'}[n])+', centered in its bounding rectangle, no lamp base, no text, thin clean silhouette. Transparent exterior.'
    if n.startswith('PRJ_Dot'): return 'A small centered flat white circular pip, no rim, no shading, no texture, no text. Transparent exterior. Neutral white allows original runtime tinting.'
    if n in ['PRJ_Ok','PRJ_No','PRJ_WIP']:
        return 'A simple high contrast white '+({'PRJ_Ok':'check mark','PRJ_No':'diagonal cross mark','PRJ_WIP':'hourglass'}[n])+', thick clear silhouette, no circle around it, no text, no texture. Transparent exterior. Runtime tint is applied by the game.'
    if n in ['Arrow','Forward']:
        return 'A clean single pale amber '+('up-pointing chevron, a thick capital lambda silhouette' if n=='Arrow' else 'right-pointing chevron')+', thick flat readable silhouette centered, transparent exterior. No text, no shadow, no additional objects.'
    if n=='Mouse': return 'A clean pale-amber arrow cursor pointing toward the top-left, solid shape with a dark indigo outline, single simple pointer, transparent exterior, no text, no hand.'
    raise ValueError(n)

def main():
    path=ROOT/'fidelity/art/jobs.json';data=json.loads(path.read_text('utf-8'))
    for j in data['jobs']:
        if j['name']=='SubTitle': j['kind']='title'
        j['final']='local/NightArt/'+Path(j['final']).name
        if (ROOT/j['raw']).exists(): j['status']='raw_generated'
        if j['kind']!='interface': continue
        p=ROOT/j['prompt'];old=p.read_text('utf-8')
        lines=old.splitlines();lines=[('Subject: '+role(j['name'])) if line.startswith('Subject:') else line for line in lines]
        # Do not ask for warm lighting on tint masks and functional marks.
        if j['name'].endswith('_Mask') or j['name'] in ['Solid','PRJ_BUTTON','CARD_04_Turn','PRJ_Ok','PRJ_No','PRJ_WIP'] or j['name'].startswith('PRJ_Dot'):
            lines=[('Style: Flat minimal functional UI mask, uniform neutral white geometry, no lighting.') if line.startswith('Style:') else line for line in lines]
        p.write_text('\n'.join(lines)+'\n',encoding='utf-8',newline='\n')
    path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
if __name__=='__main__': main()
