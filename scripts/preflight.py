"""Static package validation, NOT a substitute for compiling or live testing C# and React."""
from pathlib import Path
import json,zipfile,xml.etree.ElementTree as ET,re,subprocess
from docx import Document
from pptx import Presentation
import fitz
P=Path(__file__).resolve().parents[1]
required=[
'backend/CrisisAid.Api/Program.cs','backend/CrisisAid.Api/Data/AidDbContext.cs',
'backend/CrisisAid.Api/Domain/Entities.cs','backend/CrisisAid.Api/Services/AiDecisionService.cs',
'backend/CrisisAid.Tests/TriageTests.cs','frontend/src/App.jsx','frontend/src/pages/Citizen.jsx',
'frontend/src/pages/Requests.jsx','frontend/src/pages/Resources.jsx','frontend/src/pages/Shelters.jsx',
'frontend/src/pages/Volunteers.jsx','frontend/src/pages/Donations.jsx','frontend/src/pages/Admin.jsx',
'docker-compose.yml','database/verification_queries.sql',
'docs/Group_XX_Final_Report.docx','docs/Group_XX_Final_Report.pdf',
'docs/presentation/CrisisAid_10_Minute_Presentation.pptx',
'docs/Presentation_Video_Link_Submission.docx','postman/CrisisAid.postman_collection.json'
]
results=[]
for path in required:
 assert (P/path).is_file(), f'REQUIRED FILE MISSING: {path}'
results.append(f'PASS: {len(required)} required project artifacts exist')
for f in [P/'frontend/package.json',P/'backend/CrisisAid.Api/appsettings.json',*list((P/'postman').glob('*.json'))]:
 with f.open() as stream:json.load(stream)
results.append('PASS: npm, API settings and Postman JSON parse')
for f in (P/'backend').rglob('*.csproj'):ET.parse(f)
results.append('PASS: C# project XML parses')
for f in (P/'diagrams').glob('*.dot'):
 subprocess.run(['dot','-Tsvg',str(f),'-o','/dev/null'],check=True)
results.append('PASS: Graphviz diagram sources parse')
report=Document(P/'docs/Group_XX_Final_Report.docx')
words=sum(len(par.text.split()) for par in report.paragraphs)+sum(len(cell.text.split()) for tab in report.tables for row in tab.rows for cell in row.cells)
assert 1000<=words<=4000,(words,'report exceeds coursework word constraint')
for i in range(1,11):
 assert any(par.text.startswith(f'Chapter {i} ') for par in report.paragraphs),f'Chapter {i} missing'
results.append(f'PASS: 10 report chapters; {words} words (includes front matter and captions)')
with fitz.open(P/'docs/Group_XX_Final_Report.pdf') as pdf: assert len(pdf)>=5;report_pages=len(pdf)
slides=Presentation(P/'docs/presentation/CrisisAid_10_Minute_Presentation.pptx')
assert len(slides.slides)==10
results.append(f'PASS: {report_pages}-page report PDF; {len(slides.slides)} slides')
with zipfile.ZipFile(P/'docs/Group_XX_Final_Report.docx') as z:assert 'word/document.xml' in z.namelist()
results.append('PASS: DOCX package integrity')
assert 'RequireAuthorization' in (P/'backend/CrisisAid.Api/Program.cs').read_text()
assert 'IsolationLevel.Serializable' in (P/'backend/CrisisAid.Api/Program.cs').read_text()
assert 'ReviewStatus' in (P/'backend/CrisisAid.Api/Domain/Entities.cs').read_text()
results.append('PASS: architecture/source presence checks (not compilation)')
results.append('NOT RUN: .NET compilation/xUnit, npm build, API/SQL integration, live browser UI')
output='\n'.join(results)+'\n'
(P/'docs/STATIC_PRECHECK_RESULTS.txt').write_text(output)
print(output)
