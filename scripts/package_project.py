from pathlib import Path
import zipfile,hashlib
root=Path(__file__).resolve().parents[1]
top=root.parent
code=top/'CrisisAid_Prototype_Source_Code.zip'
complete=top/'CrisisAid_Complete_Coursework_Package.zip'
ignore={'.env','node_modules','bin','obj','dist','.git','.DS_Store'}
files=[]
for p in root.rglob('*'):
 if not p.is_file():continue
 if any(part in ignore for part in p.relative_to(root).parts):continue
 files.append(p)
with zipfile.ZipFile(code,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=8) as z:
 for p in files:
  rel=p.relative_to(root)
  if p.suffix.lower() in {'.docx','.pptx','.pdf'}: continue
  if rel.parts[0]=='docs' and rel.parts[1]=='presentation':continue
  z.write(p,str(Path(root.name)/rel))
with zipfile.ZipFile(complete,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=8) as z:
 for p in files:z.write(p,str(Path(root.name)/p.relative_to(root)))
 z.write(code,str(Path(root.name)/'Prototype_Source_Code.zip'))
for p in [code,complete]:
 with zipfile.ZipFile(p) as z:
  assert z.testzip() is None
  print(p.name, len(z.namelist()),'files',round(p.stat().st_size/1024/1024,2),'MB',hashlib.sha256(p.read_bytes()).hexdigest()[:16])
