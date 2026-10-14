param([string]$FontFile)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
if (-not $FontFile) { $FontFile = Join-Path $taskRoot 'local/UnityProject/Assets/Recovery/NightFonts/sharedassets0.assets_217.otf' }
$mediaRoot = Join-Path $taskRoot 'release/media'
$exportRoot = Join-Path $mediaRoot 'generated'
New-Item -ItemType Directory -Path $exportRoot -Force | Out-Null
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Drawing.Imaging;
public static class SteamMediaExporter {
  static FontFamily family;
  static PrivateFontCollection fonts;
  static Color ink=Color.FromArgb(255,23,31,44), gold=Color.FromArgb(255,238,208,142);
  public static void LoadFont(string path) {
    fonts=new PrivateFontCollection(); fonts.AddFontFile(path);
    if(fonts.Families.Length<1) throw new Exception("Noto font could not be loaded");
    family=fonts.Families[0];
  }
  static void Cover(Graphics g,Image image,int w,int h,RectangleF source) {
    float scale=Math.Max(w/source.Width,h/source.Height);
    float sw=w/scale,sh=h/scale;
    RectangleF sample=new RectangleF(source.X+(source.Width-sw)/2,source.Y+(source.Height-sh)/2,sw,sh);
    g.DrawImage(image,new RectangleF(0,0,w,h),sample,GraphicsUnit.Pixel);
  }
  static void Text(Graphics g,string text,float x,float y,float width,float height,float wanted) {
    float size=wanted;
    using(StringFormat format=new StringFormat(StringFormat.GenericTypographic)) {
      format.Alignment=StringAlignment.Center;format.LineAlignment=StringAlignment.Center;
      while(size>8) {using(Font probe=new Font(family,size,FontStyle.Regular,GraphicsUnit.Pixel)) {
        SizeF measured=g.MeasureString(text,probe,new SizeF(10000,10000),format);
        if(measured.Width<=width*.96f && measured.Height<=height*.92f) break;
      }size-=1;}
      using(GraphicsPath path=new GraphicsPath()) {
        path.AddString(text,family,(int)FontStyle.Regular,size,new RectangleF(x,y,width,height),format);
        using(Pen outline=new Pen(ink,Math.Max(1,size*.055f))) {outline.LineJoin=LineJoin.Round;g.DrawPath(outline,path);}
        using(Brush brush=new SolidBrush(gold))g.FillPath(brush,path);
      }
    }
  }
  public static void Export(string source,string output,int w,int h,string role) {
    using(Bitmap target=new Bitmap(w,h,PixelFormat.Format32bppArgb)) {
      using(Graphics g=Graphics.FromImage(target)) {
        g.Clear(Color.Transparent);g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;
        if(role!="logo") using(Image raw=Image.FromFile(source)) {
          RectangleF sample=new RectangleF(0,0,raw.Width,raw.Height);
          if(role=="icon")sample=new RectangleF(raw.Width*.20f,raw.Height*.25f,raw.Width*.65f,raw.Height*.52f);
          Cover(g,raw,w,h,sample);
        }
        if(role=="landscape") {
          Text(g,"\u5996\u602a\u591c\u5e02",w*.025f,h*.28f,w*.43f,h*.22f,h*.21f);
          Text(g,"\u5f00\u5f20",w*.025f,h*.51f,w*.43f,h*.25f,h*.23f);
        } else if(role=="portrait") {
          Text(g,"\u5996\u602a\u591c\u5e02\u5f00\u5f20",w*.04f,h*.045f,w*.92f,h*.13f,w*.13f);
        } else if(role=="logo") {
          Text(g,"\u5996\u602a\u591c\u5e02\u5f00\u5f20",w*.03f,h*.15f,w*.94f,h*.70f,w*.15f);
        }
      }
      target.Save(output,output.EndsWith(".jpg")?ImageFormat.Jpeg:ImageFormat.Png);
    }
  }
}
'@
[SteamMediaExporter]::LoadFont((Resolve-Path -LiteralPath $FontFile).Path)
$landscape = (Resolve-Path -LiteralPath (Join-Path $mediaRoot 'source/landscape-master-v1.raw.png')).Path
$portrait = (Resolve-Path -LiteralPath (Join-Path $mediaRoot 'source/portrait-master-v1.raw.png')).Path
$spec = Get-Content -LiteralPath (Join-Path $mediaRoot 'specs.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($asset in $spec.assets) {
    $role = 'landscape'; $source = $landscape
    if ($asset.id -in @('vertical','library-capsule')) { $role='portrait'; $source=$portrait }
    if ($asset.id -eq 'library-hero') { $role='art' }
    if ($asset.id -eq 'library-logo') { $role='logo' }
    if ($asset.id -in @('shortcut-icon','app-icon')) { $role='icon'; $source=$portrait }
    $target = Join-Path $exportRoot ($asset.id + '.' + $asset.format)
    [SteamMediaExporter]::Export($source,$target,$asset.width,$asset.height,$role)
}
Write-Output 'Exported 9 candidate Steam assets with uniform cover sampling and native Noto title; no runtime game assets changed.'
