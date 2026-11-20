from PIL import Image,ImageDraw,ImageFont
from pathlib import Path
out=Path(__file__).resolve().parents[1]/'diagrams'
font='/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf'
bold='/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf'
def draw(title,participants,steps,filename):
 W=1560; H=250+max(7,len(steps))*90
 img=Image.new('RGB',(W,H),'#ffffff');d=ImageDraw.Draw(img)
 f12=ImageFont.truetype(font,21);fheader=ImageFont.truetype(bold,21);ft=ImageFont.truetype(bold,32)
 navy='#18364A';gray='#8296A3';teal='#258D91'
 d.rounded_rectangle((20,18,W-20,78),radius=12,fill='#eaf5f5');d.text((42,31),title,font=ft,fill=navy)
 xs=[95+(W-190)*i/(len(participants)-1) for i in range(len(participants))]
 for x,name in zip(xs,participants):
  d.rounded_rectangle((int(x-115),110,int(x+115),171),radius=10,fill='#e3eef4',outline='#89b9c0',width=2)
  box=d.textbbox((0,0),name,font=fheader)
  d.text((x-(box[2]-box[0])/2,128),name,font=fheader,fill=navy)
  for y in range(179,H-28,18): d.line((x,y,x,y+9),fill='#a6becb',width=2)
 for i,(src,dst,message,tone) in enumerate(steps):
  y=225+i*85; a,b=xs[src],xs[dst]; color=teal if tone=='ok' else ('#d48547' if tone=='wait' else '#647f91')
  if src==dst:
   d.arc((int(a-65),y-14,int(a+65),y+30),270,90,fill=color,width=3)
   d.line((a+65,y+12,a+54,y+2),fill=color,width=3)
  else:
   d.line((a,y,b,y),fill=color,width=3)
   if b>a: d.polygon([(b,y),(b-12,y-6),(b-12,y+6)],fill=color)
   else: d.polygon([(b,y),(b+12,y-6),(b+12,y+6)],fill=color)
  tw=d.textbbox((0,0),message,font=f12)[2]
  # Text background for legibility
  tx=(a+b)/2-tw/2 if src!=dst else a+30
  d.rounded_rectangle((tx-7,y-33,tx+tw+7,y-4),radius=5,fill='white')
  d.text((tx,y-33),message,font=f12,fill=navy)
  d.text((35,y-9),f'{i+1:02}',font=f12,fill=gray)
 img.save(out/filename)

draw('Sequence 1 • Submit assistance request',['Citizen','React','API','SQL Server'],[
 (0,1,'Enter needs + location','ok'),(1,2,'POST /requests + JWT','ok'),(2,2,'Validate crisis and input','wait'),(2,3,'Save request, history and audit','ok'),(3,2,'Transaction committed','ok'),(2,1,'201 Created + case ID','ok'),(1,0,'Track: Submitted','ok')],'07_submit_request.png')
draw('Sequence 2 • AI recommendation & human review',['Coordinator','Case API','AI Service','SQL Server'],[
 (0,1,'Request assessment','ok'),(1,2,'Evaluate and classify','ok'),(2,2,'External model or fallback','wait'),(2,1,'Priority + confidence + reason','ok'),(1,3,'Store recommendation Pending','ok'),(0,1,'Approve / modify / reject','ok'),(1,3,'Store actor, action, time, audit','ok'),(1,0,'Reviewed decision','ok')],'08_ai_human_review.png')
draw('Sequence 3 • Resource allocation / shortage',['Logistics','API','SQL Server','Outbox'],[
 (0,1,'Approve allocation','ok'),(1,2,'Begin SERIALIZABLE transaction','ok'),(1,2,'Check approval + constraints','wait'),(2,1,'Stock sufficient?','wait'),(1,2,'Reserve + decrement + audit','ok'),(1,3,'Queue idempotent event','ok'),(2,1,'Commit or 409 on shortage','ok'),(3,0,'In-app / SMTP status update','ok')],'09_resource_shortage.png')
draw('Sequence 4 • Volunteer task assignment',['Coordinator','Volunteer API','SQL Server','Volunteer'],[
 (0,1,'Assign skill-based task','ok'),(1,2,'Check skills, verification, availability','wait'),(1,2,'Create task + audit + notification','ok'),(1,0,'Assignment accepted or 409','ok'),(3,1,'Accept task','ok'),(1,2,'State: Accepted','ok'),(3,1,'Complete task','ok'),(1,2,'State: Completed; available','ok')],'10_volunteer_assignment.png')
print('Rendered sequence diagram PNGs')
