from pptx import Presentation
from pptx.util import Inches,Pt
from pptx.enum.shapes import MSO_SHAPE
from pptx.enum.text import PP_ALIGN,MSO_ANCHOR
from pptx.dml.color import RGBColor
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'docs'/'presentation';OUT.mkdir(parents=True,exist_ok=True)
p=Presentation();p.slide_width=Inches(13.333);p.slide_height=Inches(7.5)
NAVY=RGBColor(16,42,61);TEAL=RGBColor(29,155,146);SOFT=RGBColor(234,246,244);WHITE=RGBColor(255,255,255);INK=RGBColor(31,64,79);MUTED=RGBColor(102,125,141);PALE=RGBColor(246,249,252);AMBER=RGBColor(237,162,75)
def rect(s,x,y,w,h,color,round=False,line=None):
 sh=s.shapes.add_shape(MSO_SHAPE.ROUNDED_RECTANGLE if round else MSO_SHAPE.RECTANGLE,Inches(x),Inches(y),Inches(w),Inches(h));sh.fill.solid();sh.fill.fore_color.rgb=color;sh.line.fill.background() if line is None else None
 if line:sh.line.color.rgb=line
 return sh
def txt(s,t,x,y,w,h,size=18,color=INK,bold=False,font='Aptos',align=None):
 sh=s.shapes.add_textbox(Inches(x),Inches(y),Inches(w),Inches(h));tf=sh.text_frame;tf.clear();tf.word_wrap=True
 for i,line in enumerate(t.split('\n')):
  par=tf.paragraphs[0] if i==0 else tf.add_paragraph();par.text=line
  if align:par.alignment=align
  for r in par.runs:r.font.name=font;r.font.size=Pt(size);r.font.color.rgb=color;r.font.bold=bold
 return sh
def base(n,time,kind='DEMONSTRATION'):
 s=p.slides.add_slide(p.slide_layouts[6]);s.background.fill.solid();s.background.fill.fore_color.rgb=PALE
 rect(s,0,0,13.333,.12,TEAL)
 txt(s,'CRISISAID',.58,.30,3,.35,13,NAVY,True)
 txt(s,'SE205.3  /  SOFTWARE ARCHITECTURE 2026',8.2,.33,4.5,.28,9,MUTED,True)
 rect(s,.55,7.03,12.23,.015,RGBColor(218,230,237))
 txt(s,'NSBM  •  GROUP [NUMBER]  •  PLACEHOLDERS REQUIRE REAL EVIDENCE',.58,7.1,8,.22,8,MUTED)
 txt(s,f'{kind}  |  {time}  |  {n}/10',9.1,7.08,3.65,.26,8,TEAL,True,align=PP_ALIGN.RIGHT)
 return s
def heading(s,kicker,title,desc=''):
 txt(s,kicker,.65,.95,7,.24,11,TEAL,True)
 txt(s,title,.65,1.31,11.9,.68,30,NAVY,True)
 if desc:txt(s,desc,.66,2.06,11.9,.5,15,MUTED)
def pill(s,label,x,y,w,color=SOFT):
 rect(s,x,y,w,.43,color,True);txt(s,label,x+.1,y+.1,w-.2,.23,10,NAVY,True)
def bulletcard(s,x,y,w,title,body,accent=TEAL):
 rect(s,x,y,w,1.45,WHITE,True)
 rect(s,x+.14,y+.18,.07,1.06,accent,True)
 txt(s,title,x+.37,y+.22,w-.55,.3,15,INK,True)
 txt(s,body,x+.37,y+.58,w-.57,.73,11.5,MUTED)

# Slide1: problem and goals
s=base(1,'00:00–00:55');rect(s,8.4,.93,4.2,5.64,NAVY,True)
txt(s,'MISSION / PROBLEM',.65,.95,7,.24,11,TEAL,True)
txt(s,'One crisis. Many organisations.\nOne shared response.',.65,1.32,7.4,1.07,28,NAVY,True)
txt(s,'An AI-assisted platform for coordinating assistance with human accountability.',.66,2.49,7.24,.43,13,MUTED)
pill(s,'ASSISTANCE REQUESTS',.72,3.05,2.45);pill(s,'HUMAN-REVIEWED AI',3.31,3.05,2.35);pill(s,'AUDITABLE ALLOCATION',5.81,3.05,2.4)
txt(s,'KEY PROBLEM',.79,4.0,2.1,.27,11,TEAL,True)
txt(s,'Scattered records, duplicate reports, constrained supplies and unclear decision ownership delay emergency relief.',.78,4.39,7.13,1.2,19,INK,True)
for y,num,title,desc in [(1.54,'01','Capture','Requests & location'),(2.73,'02','Review','AI supports staff'),(3.92,'03','Coordinate','Supplies & people'),(5.11,'04','Audit','Every critical action')]:
 txt(s,num,8.78,y,.6,.35,22,RGBColor(97,221,196),True)
 txt(s,title,9.54,y,2.6,.33,18,WHITE,True)
 txt(s,desc,9.54,y+.4,2.65,.25,11,RGBColor(183,209,216))

# Slide2 architecture
s=base(2,'00:55–01:55');heading(s,'ARCHITECTURE','One coherent, modular platform','React + ASP.NET Core + SQL Server; REST interfaces and resilient integrations.')
rect(s,.65,2.58,9.0,3.99,WHITE,True)
s.shapes.add_picture(str(ROOT/'diagrams/01_architecture.png'),Inches(.82),Inches(2.7),width=Inches(8.6),height=Inches(3.73))
bulletcard(s,9.88,2.66,2.75,'Core principles','Layer separation, interfaces, dependency injection.',TEAL)
bulletcard(s,9.88,4.32,2.75,'Data integrity','SQL transactions, outbox and auditable approvals.',AMBER)

# Slide3 case + AI
s=base(3,'01:55–02:55');heading(s,'LIVE WALKTHROUGH / PART 1','From request to human-reviewed priority','Demonstrate these steps in the running app during the recording.')
for i,(label,title,body) in enumerate([('01','CITIZEN','Submit needs, urgency, quantity and location.'),('02','AI ADVICE','Show category, score, confidence and rationale.'),('03','HUMAN CONTROL','Approve, modify or reject with a recorded reason.')]):
 x=.73+i*4.23;rect(s,x,2.7,3.98,1.68,WHITE,True);txt(s,label,x+.22,2.94,.7,.35,22,TEAL,True);txt(s,title,x+1.02,2.89,2.7,.36,16,NAVY,True);txt(s,body,x+.23,3.47,3.5,.6,12,MUTED)
rect(s,.75,4.75,11.8,1.33,NAVY,True)
txt(s,'GOVERNANCE RULE',1.0,4.99,2.7,.3,12,RGBColor(108,224,206),True)
txt(s,'AI never authorises aid dispatch. The original recommendation and every human override remain traceable.',1.0,5.35,11.1,.5,16,WHITE,True)

# Slide4 logistics demo
s=base(4,'02:55–04:00');heading(s,'LIVE WALKTHROUGH / PART 2','Allocate responsibly. Then prove it.','Use a prepared verified case and show a real stock shortage conflict.')
bulletcard(s,.7,2.59,5.92,'01 / Resource allocation','Match stock by category, accessibility, available quantity and distance.')
bulletcard(s,6.73,2.59,5.89,'02 / Shelter & volunteer','Demonstrate occupancy limits, skills and verification.')
bulletcard(s,.7,4.26,5.92,'03 / Notification outbox','Show resident in-app message and safe email fallback.')
bulletcard(s,6.73,4.26,5.89,'04 / Audit and metrics','Show the actor, action, timestamp and original AI decision.')
txt(s,'Required demo proof: React screens  •  Swagger/Postman  •  SQL Server',.77,6.39,11.3,.3,12,TEAL,True)

members=[
 ('FRONTEND / EXPERIENCE','Role-based React client','Show actual screens and code you implemented. Explain component structure, responsiveness and validation.','Potential risk: mobile usability / form errors. Evidence: Git commit and UI demonstration.'),
 ('BACKEND / SECURITY','REST API and access control','Demonstrate actual endpoints, JWT login and role checks. Explain DTO boundaries and HTTP status contracts.','Potential risk: unauthorised actions. Evidence: negative API test and Git commits.'),
 ('DATA / CONSISTENCY','SQL Server and transactions','Demonstrate actual EF Core entities, relations, concurrency-safe allocation and verification queries.','Potential risk: overselling stock under concurrency. Evidence: SQL transaction test and commits.'),
 ('AI / GOVERNANCE','Explainable triage and human oversight','Show actual recommendation generation, fallback, override reason and decision audit record.','Potential risk: wrong AI advice or private data exposure. Evidence: fallback test and review logic.'),
 ('FIELD OPERATIONS','Logistics, shelters and volunteers','Show actual matching, capacity checks, volunteer task verification and state transitions.','Potential risk: conflicting allocations. Evidence: shortage/capacity test and commits.'),
 ('INTEGRATION / QA','Notifications, maps and verification','Show actual outbox, OpenStreetMap geocoding, Postman collection, diagram and test case.','Potential risk: external-service outage. Evidence: fallback test / test matrix / commits.')
]
for i,(tag,focus,describe,risk) in enumerate(members,1):
 s=base(4+i,f'{3+i:02}:00–{4+i:02}:00','INDIVIDUAL CONTRIBUTION')
 heading(s,f'MEMBER {i} / {tag}',f'[NAME]  •  [INDEX NUMBER]','Replace this slide with the real contribution of the person speaking.')
 rect(s,.77,2.73,8.15,3.25,WHITE,True);rect(s,.77,2.73,.10,3.25,TEAL)
 txt(s,focus,1.18,3.11,7.3,.48,23,NAVY,True)
 txt(s,describe,1.18,3.85,7.12,1.27,16,INK)
 rect(s,9.18,2.73,3.4,3.25,NAVY,True)
 txt(s,'SHOW LIVE',9.51,3.1,2.58,.26,11,RGBColor(117,225,208),True)
 txt(s,'[REAL FUNCTION]\n[CODE FILE / SCREEN]\n[COMMIT / TEST]',9.51,3.54,2.65,1.37,15,WHITE,True)
 txt(s,risk,.78,6.26,11.6,.38,12,MUTED)

p.save(OUT/'CrisisAid_10_Minute_Presentation.pptx')
print('Slides created:',len(p.slides),OUT/'CrisisAid_10_Minute_Presentation.pptx')
