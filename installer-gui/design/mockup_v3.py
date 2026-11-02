from PIL import Image, ImageDraw, ImageFont, ImageFilter
import math
S = 2
BG=(14,13,15); WIN=(25,24,27); CARD=(37,36,40); CARD2=(50,48,54); BORDER=(55,53,59)
PINK=(255,186,216); TXT=(246,244,247); SUB=(152,149,158); DARK=(43,20,33); DIM=(92,89,98)
MINT=(146,224,178); MINT_TINT=(31,52,42); MINT_LINE=(62,112,84); MINT_DARK=(16,46,30); MINT_SUB=(50,98,68)
RED=(240,128,134); RED_TEXT=(58,16,22); THUMB=(88,86,95)
SANS='/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf'; SANSB='/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf'
MONO='/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf'; MONOB='/usr/share/fonts/truetype/dejavu/DejaVuSansMono-Bold.ttf'
def F(p,z): return ImageFont.truetype(p,int(z*S))
def s(v): return int(round(v*S))
def rr(d,box,r,fill=None,outline=None,width=1):
    x0,y0,x1,y1=box; d.rounded_rectangle([s(x0),s(y0),s(x1),s(y1)],radius=s(r),fill=fill,outline=outline,width=s(width) if outline else 0)
def tl(d,x,cy,t,font,fill): d.text((s(x),s(cy)),t,font=font,fill=fill,anchor='lm')
def tc(d,cx,cy,t,font,fill): d.text((s(cx),s(cy)),t,font=font,fill=fill,anchor='mm')
def line(d,pts,color,w):
    P=[(s(x),s(y)) for x,y in pts]; d.line(P,fill=color,width=s(w),joint='curve')
    for p in (P[0],P[-1]):
        r=s(w)/2; d.ellipse([p[0]-r,p[1]-r,p[0]+r,p[1]+r],fill=color)
def glow(im,box,r,color,blur,alpha=170,ellipse=False):
    gl=Image.new('RGBA',im.size,(0,0,0,0)); g=ImageDraw.Draw(gl); x0,y0,x1,y1=box
    if ellipse: g.ellipse([s(x0),s(y0),s(x1),s(y1)],fill=color+(alpha,))
    else: g.rounded_rectangle([s(x0),s(y0),s(x1),s(y1)],radius=s(r),fill=color+(alpha,))
    gl=gl.filter(ImageFilter.GaussianBlur(s(blur))); im.alpha_composite(gl)

# ---- icons ----
def thin_check(d,cx,cy,r,color,w): line(d,[(cx-0.62*r,cy+0.08*r),(cx-0.2*r,cy+0.5*r),(cx+0.62*r,cy-0.5*r)],color,w)
def icon_check(d,cx,cy,r,color,w=2.4): line(d,[(cx-0.52*r,cy+0.02*r),(cx-0.1*r,cy+0.44*r),(cx+0.56*r,cy-0.42*r)],color,w)
def icon_gear(d,cx,cy,r,color,bg):
    for k in range(8):
        th=k*math.pi/4; ux,uy=math.cos(th),math.sin(th); px,py=-uy,ux
        pts=[(cx+ux*0.55*r+px*0.2*r,cy+uy*0.55*r+py*0.2*r),(cx+ux*r+px*0.13*r,cy+uy*r+py*0.13*r),(cx+ux*r-px*0.13*r,cy+uy*r-py*0.13*r),(cx+ux*0.55*r-px*0.2*r,cy+uy*0.55*r-py*0.2*r)]
        d.polygon([(s(x),s(y)) for x,y in pts],fill=color)
    d.ellipse([s(cx-0.72*r),s(cy-0.72*r),s(cx+0.72*r),s(cy+0.72*r)],fill=color); d.ellipse([s(cx-0.3*r),s(cy-0.3*r),s(cx+0.3*r),s(cy+0.3*r)],fill=bg)
def icon_globe(d,cx,cy,r,color,w=1.8):
    d.ellipse([s(cx-r),s(cy-r),s(cx+r),s(cy+r)],outline=color,width=s(w)); d.ellipse([s(cx-0.45*r),s(cy-r),s(cx+0.45*r),s(cy+r)],outline=color,width=s(w*0.8)); line(d,[(cx-r,cy),(cx+r,cy)],color,w*0.8)
def icon_folder(d,cx,cy,r,color,w=1.9):
    rr(d,(cx-r,cy-0.55*r,cx-0.1*r,cy-0.05*r),0.18*r,outline=color,width=w); rr(d,(cx-r,cy-0.3*r,cx+r,cy+0.72*r),0.2*r,outline=color,width=w)
def icon_refresh(d,cx,cy,r,color,w=2.2):
    d.arc([s(cx-r),s(cy-r),s(cx+r),s(cy+r)],start=30,end=300,fill=color,width=s(w)); a=math.radians(300); ex,ey=cx+r*math.cos(a),cy+r*math.sin(a)
    d.polygon([(s(ex),s(ey-0.55*r)),(s(ex+0.62*r),s(ey+0.1*r)),(s(ex-0.55*r),s(ey+0.25*r))],fill=color)
def icon_search(d,cx,cy,r,color,w=2.0):
    d.ellipse([s(cx-r),s(cy-r),s(cx+r*0.7),s(cy+r*0.7)],outline=color,width=s(w)); line(d,[(cx+r*0.5,cy+r*0.5),(cx+r*1.05,cy+r*1.05)],color,w)
def chev(d,cx,cy,r,color,direction,w=2.2):
    pts={'left':[(cx+0.35*r,cy-0.7*r),(cx-0.35*r,cy),(cx+0.35*r,cy+0.7*r)],'down':[(cx-0.7*r,cy-0.3*r),(cx,cy+0.4*r),(cx+0.7*r,cy-0.3*r)],'up':[(cx-0.7*r,cy+0.3*r),(cx,cy-0.4*r),(cx+0.7*r,cy+0.3*r)]}[direction]
    line(d,pts,color,w)
def icon_btn(d,x,y,kind,size=40):
    rr(d,(x,y,x+size,y+size),12,fill=CARD2); cx,cy=x+size/2,y+size/2
    if kind=='gear': icon_gear(d,cx,cy,9,TXT,CARD2)
    elif kind=='about': d.ellipse([s(cx-10),s(cy-10),s(cx+10),s(cy+10)],outline=TXT,width=s(1.9)); icon_check(d,cx,cy+0.5,5.2,TXT,2.0)
    elif kind=='back': chev(d,cx,cy,8,TXT,'left',2.4)
    elif kind=='min': line(d,[(cx-7,cy+1),(cx+7,cy+1)],TXT,2.0)
    elif kind=='close': line(d,[(cx-6,cy-6),(cx+6,cy+6)],TXT,2.0); line(d,[(cx-6,cy+6),(cx+6,cy-6)],TXT,2.0)
def tile(d,x,y,kind):
    rr(d,(x,y,x+36,y+36),11,fill=CARD2); cx,cy=x+18,y+18
    {'globe':lambda:icon_globe(d,cx,cy,9,PINK),'refresh':lambda:icon_refresh(d,cx,cy,8,PINK,2.0),'folder':lambda:icon_folder(d,cx,cy,9,PINK)}[kind]()
def logo(d,x,y,size,radius,thick):
    rr(d,(x,y,x+size,y+size),radius,fill=PINK); thin_check(d,x+size/2,y+size/2,size*0.27,DARK,thick)

W,H=440,716
def new_window():
    im=Image.new('RGBA',(s(W),s(H)),(0,0,0,0)); d=ImageDraw.Draw(im); rr(d,(0,0,W-1,H-1),22,fill=WIN+(255,),outline=BORDER,width=1); return im,d

def group_header(d,y,version,installed,usable,open_):
    rr(d,(24,y,W-24,y+64),18,fill=MINT_TINT if installed else CARD,outline=MINT_LINE if installed else None,width=1)
    tl(d,44,y+32,version,F(MONOB,18),MINT if installed else (TXT if usable else DIM))
    chev(d,W-24-26,y+32,6.5,MINT if installed else (SUB if usable else DIM),'up' if open_ else 'down',2.0)
def patch_card(d,y,title,sub,installed=False,selected=False):
    bg=MINT if installed else (PINK if selected else CARD2); tcol=MINT_DARK if installed else (DARK if selected else TXT); scol=MINT_SUB if installed else (PINK_SUB if selected else SUB)
    rr(d,(34,y,W-24,y+72),16,fill=bg); tl(d,54,y+26,title,F(SANSB,16.5),tcol); tl(d,54,y+51,sub,F(MONO,12),scol)
    cx,cy=W-24-30,y+36
    if installed: d.ellipse([s(cx-14),s(cy-14),s(cx+14),s(cy+14)],fill=MINT_DARK); icon_check(d,cx,cy,6.5,MINT,2.3)
    elif selected: d.ellipse([s(cx-14),s(cy-14),s(cx+14),s(cy+14)],fill=DARK); icon_check(d,cx,cy,6.5,PINK,2.3)
    else: d.ellipse([s(cx-14),s(cy-14),s(cx+14),s(cy+14)],outline=(84,82,90),width=s(1.8))
PINK_SUB=(122,66,94)

def main_window():
    im,d=new_window()
    logo(d,24,25,30,8,2.0); tl(d,66,40,'osu!lazer Fix',F(SANSB,17),TXT)
    icon_btn(d,224,20,'gear'); icon_btn(d,272,20,'about'); icon_btn(d,328,20,'min'); icon_btn(d,376,20,'close')
    # status card with a glowing green dot
    rr(d,(24,84,W-24,170),18,fill=CARD)
    tl(d,44,106,'Game',F(SANS,11.5),SUB); tl(d,44,138,'2026.1005.0',F(MONOB,21),TXT)
    tl(d,214,106,'Status',F(SANS,11.5),SUB)
    glow(im,(209,129,229,149),0,MINT,6,alpha=230,ellipse=True); d=ImageDraw.Draw(im)
    d.ellipse([s(214),s(134),s(224),s(144)],fill=MINT); tl(d,234,139,'Online installed',F(MONO,13.5),MINT)
    # search
    rr(d,(24,184,W-24,226),14,fill=CARD); icon_search(d,48,204,7,SUB,2.0); tl(d,70,205,'Search version',F(SANS,14),DIM)
    # version tabs
    y=240
    group_header(d,y,'2026.1005.0',True,True,True)
    patch_card(d,y+72,'Online','1000 FPS',installed=True)
    patch_card(d,y+152,'Partial offline','2000 FPS')
    y2=y+232
    group_header(d,y2,'2025.0925.0',False,False,False)
    group_header(d,y2+74,'2025.0801.0',False,False,False)
    # scroll bar (own, thin): track + thumb at the right edge
    rr(d,(W-14,244,W-8,620),3,fill=CARD); rr(d,(W-14,248,W-8,436),3,fill=THUMB)
    # bottom fade so the cut-off tab looks scrolled
    # actions
    rr(d,(24,632,300,684),16,fill=MINT); tc(d,162,658,'Installed',F(SANSB,17),MINT_DARK)
    glow(im,(310,634,W-24,682),16,RED,10,alpha=120); d=ImageDraw.Draw(im)
    rr(d,(310,632,W-24,684),16,fill=RED); tc(d,363,658,'Remove',F(SANS,15),RED_TEXT)
    return im

def row(d,y,kind,label):
    rr(d,(24,y,W-24,y+76),18,fill=CARD); tile(d,40,y+20,kind); tl(d,90,y+38,label,F(SANSB,15),TXT)
def settings_window():
    im,d=new_window()
    icon_btn(d,24,20,'back'); tl(d,78,40,'Settings',F(SANSB,17),TXT)
    row(d,84,'globe','Language'); rr(d,(W-40-136,102,W-40,142),12,fill=CARD2); tl(d,W-40-120,122,'English',F(SANS,14),TXT); chev(d,W-62,122,6,SUB,'down',2.0)
    row(d,172,'refresh','Auto-update'); rr(d,(W-70,195,W-40,225),9,fill=PINK); icon_check(d,W-55,210,6.5,DARK,2.4)
    rr(d,(24,260,W-24,308),14,fill=CARD2); icon_refresh(d,W/2-84,284,7.5,PINK,2.0); tl(d,W/2-66,284,'Check for updates',F(SANSB,14.5),TXT)
    rr(d,(24,320,W-24,440),18,fill=CARD); tile(d,40,340,'folder'); tl(d,90,358,'Game folder',F(SANSB,15),TXT)
    rr(d,(40,392,W-40-54,430),11,fill=CARD2); tl(d,54,411,r'C:\Users\iwili\AppData\Local\osulazer',F(MONO,11.5),SUB)
    rr(d,(W-40-46,392,W-40,430),11,fill=PINK); icon_folder(d,W-40-23,411,7.5,DARK,1.8)
    return im
def about_window():
    im,d=new_window()
    icon_btn(d,24,20,'back'); tl(d,78,40,'About',F(SANSB,17),TXT)
    logo(d,W/2-52,150,104,26,4.2)
    tc(d,W/2,286,'osu!lazer Fix',F(SANSB,24),TXT); tc(d,W/2,326,'v1.0.0',F(MONO,15),SUB); tc(d,W/2,358,'by iwili',F(SANS,16),TXT)
    rr(d,(W/2-90,416,W/2+90,464),16,fill=CARD2); tc(d,W/2,440,'GitHub',F(SANSB,15),TXT)
    tc(d,W/2,H-44,'Made for osu!lazer',F(SANS,12),DIM)
    return im

MARGIN,GAP=56,44; CW=MARGIN*2+W*3+GAP*2; CH=60+H+74
canvas=Image.new('RGBA',(s(CW),s(CH)),BG+(255,)); g=ImageDraw.Draw(canvas)
for yy in range(s(CH)):
    t=yy/s(CH); c=int(14+10*(1-t)); g.line([(0,yy),(s(CW),yy)],fill=(c,c,c+2,255))
for i,(name,im) in enumerate([('Main',main_window()),('Settings',settings_window()),('About',about_window())]):
    x=MARGIN+i*(W+GAP); y=60
    sh=Image.new('RGBA',canvas.size,(0,0,0,0)); sd=ImageDraw.Draw(sh); sd.rounded_rectangle([s(x),s(y+18),s(x+W),s(y+H+18)],radius=s(24),fill=(0,0,0,170))
    sh=sh.filter(ImageFilter.GaussianBlur(s(22))); canvas=Image.alpha_composite(canvas,sh); canvas.alpha_composite(im,(s(x),s(y)))
    ImageDraw.Draw(canvas).text((s(x+W/2),s(y+H+32)),name,font=F(SANS,14),fill=SUB,anchor='mm')
out=canvas.convert('RGB').resize((CW,CH),Image.LANCZOS); out.save('mockup-v3.png',optimize=True); print(out.size)
