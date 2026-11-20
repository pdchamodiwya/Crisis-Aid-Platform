from docx import Document
from docx.shared import Inches,Pt,RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT,WD_CELL_VERTICAL_ALIGNMENT
from docx.enum.style import WD_STYLE_TYPE
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.enum.section import WD_SECTION_START
from pathlib import Path
import re
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'docs'
D=Document();sec=D.sections[0];sec.page_height=Inches(11.69);sec.page_width=Inches(8.27);sec.top_margin=Inches(.82);sec.bottom_margin=Inches(.75);sec.left_margin=Inches(.82);sec.right_margin=Inches(.76)
sec.different_first_page_header_footer=True
navy=RGBColor(26,58,78); teal=RGBColor(30,134,135); muted=RGBColor(93,116,132)
style=D.styles['Normal'];style.font.name='Aptos';style.font.size=Pt(10.2);style.font.color.rgb=navy;style.paragraph_format.line_spacing=1.15;style.paragraph_format.space_after=Pt(6)
for n,pt in [('Title',24),('Heading 1',15),('Heading 2',11.5),('Heading 3',10.5)]:
 s=D.styles[n];s.font.name='Aptos Display' if n!='Heading 3' else 'Aptos';s.font.size=Pt(pt);s.font.bold=True;s.font.color.rgb=teal if n=='Heading 2' else navy;s.paragraph_format.space_before=Pt(11 if n!='Title' else 0);s.paragraph_format.space_after=Pt(5)
head=sec.header.paragraphs[0];head.alignment=WD_ALIGN_PARAGRAPH.RIGHT;rh=head.add_run('SE205.3  |  HUMANITARIAN CRISIS MANAGEMENT');rh.bold=True;rh.font.size=Pt(8);rh.font.color.rgb=muted
foot=sec.footer.paragraphs[0];foot.alignment=WD_ALIGN_PARAGRAPH.CENTER
foot.add_run('CrisisAid  •  SE205.3  |  Page ')
fld=OxmlElement('w:fldSimple');fld.set(qn('w:instr'),'PAGE');foot._p.append(fld)

def paragraph(t, style=None):
 p=D.add_paragraph(style=style);p.add_run(t);return p

def title(t): D.add_heading(t,level=1)
def sub(n,t): D.add_heading(f'{n} {t}',level=2)
def table(headers,rows,widths=None):
 T=D.add_table(rows=1,cols=len(headers));T.alignment=WD_TABLE_ALIGNMENT.CENTER;T.style='Light Shading Accent 1'
 for i,h in enumerate(headers): T.rows[0].cells[i].text=h
 for data in rows:
  cells=T.add_row().cells
  for i,value in enumerate(data):cells[i].text=str(value)
 for row in T.rows:
  for cell in row.cells:
   cell.vertical_alignment=WD_CELL_VERTICAL_ALIGNMENT.CENTER
   for p in cell.paragraphs:
    p.paragraph_format.space_after=Pt(2)
    for r in p.runs:r.font.size=Pt(8.2)
 D.add_paragraph('')
 return T

def fig(filename,caption,width=6.35):
 path=ROOT/'diagrams'/filename
 p=D.add_paragraph();p.alignment=WD_ALIGN_PARAGRAPH.CENTER
 p.add_run().add_picture(str(path),width=Inches(width))
 cap=D.add_paragraph(caption);cap.alignment=WD_ALIGN_PARAGRAPH.CENTER
 for r in cap.runs:r.italic=True;r.font.color.rgb=muted;r.font.size=Pt(8)

# Cover
for _ in range(4):D.add_paragraph('')
p=D.add_paragraph();p.alignment=WD_ALIGN_PARAGRAPH.CENTER;r=p.add_run('NSBM GREEN UNIVERSITY  |  SE205.3');r.bold=True;r.font.color.rgb=teal;r.font.size=Pt(13)
D.add_paragraph('')
p=D.add_paragraph(style='Title');p.alignment=WD_ALIGN_PARAGRAPH.CENTER;p.add_run('AI-Powered Humanitarian Crisis\nManagement and Aid Coordination\nPlatform')
p=D.add_paragraph();p.alignment=WD_ALIGN_PARAGRAPH.CENTER;r=p.add_run('CRISISAID  •  SOFTWARE ARCHITECTURE COURSEWORK 2026');r.bold=True;r.font.color.rgb=teal;r.font.size=Pt(11)
for _ in range(3):D.add_paragraph('')
for line in ['Group number: [GROUP_NUMBER]','Members and index numbers: [ADD REAL DETAILS]','Submission date: 30 November 2026','Status: Implementation package and report draft — requires runtime verification and real evidence']:
 p=D.add_paragraph();p.alignment=WD_ALIGN_PARAGRAPH.CENTER;p.add_run(line)
D.add_page_break()

title('Table of Contents')
for i,x in enumerate(['Introduction','Requirements Analysis','System Architecture','System Design','Design Patterns and Architectural Decisions','AI Decision Support','Implementation','Testing','System Demonstration and Evaluation','Individual Contributions and Source Links'],1):
 p=D.add_paragraph(f'Chapter {i}  —  {x}');p.paragraph_format.space_after=Pt(6)
paragraph('Note: chapter and subsection headings use native Word styles. Update page references in Word before submission if the report is edited.')
title('List of Figures and Tables')
paragraph('Figure 1: Architecture overview; Figure 2: Use cases; Figure 3: Class diagram; Figure 4: Entity relationship diagram; Figure 5: Component diagram; Figure 6: Deployment; Figures 7–10: Core sequence workflows.')
paragraph('Table 1: Functional requirements; Table 2: Non-functional requirements; Table 3: API contracts; Table 4: Test matrix; Table 5: Demonstration evidence checklist.')

# chapter 1
D.add_heading('Chapter 1 – Introduction',1)
sub('1.1','Background');paragraph('Large-scale floods, displacement, landslides and disease outbreaks require cooperation between residents, government teams, non-governmental organisations, shelter officers, volunteers and donors. Fragmented records can delay response, create conflicting assignments and conceal resource shortages. The SE205.3 coursework specified a multi-organisation software platform that receives, verifies, prioritises, assigns, fulfils and audits assistance requests.')
sub('1.2','Problem Statement');paragraph('Emergency case handling is often difficult when organisations work from separate lists. Staff need a clear case history, verified human approval before critical allocation, reliable notification delivery and transparent handling of AI uncertainty. The prototype addresses these operational problems without claiming to replace professional humanitarian assessment.')
sub('1.3','Proposed Solution');paragraph('CrisisAid was designed as a responsive React portal with an ASP.NET Core REST API and SQL Server persistence. Citizens can submit location-linked requests; coordinators review explainable AI recommendations; authorised personnel reserve stock, allocate shelter capacity and coordinate verified volunteers. An audit history records decisions and a transactional outbox supports asynchronous notifications. External mapping and optional AI/SMTP integrations are isolated from core workflows.')
sub('1.4','Aim and Objectives');paragraph('The aim was to deliver a coherent prototype demonstrating maintainable architecture and human-controlled emergency coordination. Objectives were: (1) implement authenticated role-based workflows; (2) support case creation and status tracking; (3) expose AI recommendations, confidence and human overrides; (4) prevent over-allocation of scarce stock and shelter spaces; (5) coordinate volunteers, donation pledges and notifications; (6) expose operational metrics and a traceable audit trail; and (7) document and prepare repeatable tests.')
sub('1.5','Scope');paragraph('The implemented demonstration scope covers one SQL Server-backed API, a modern browser client, seeded fictional crisis data, OpenStreetMap-based location display and optional external AI/SMTP connections. It excludes IoT and hardware, live payment collection, real emergency dispatch, verified mass translation and professional deployment certification. Final academic evidence and source/recording URLs require completion by the student group.')

D.add_heading('Chapter 2 – Requirements Analysis',1)
sub('2.1','Functional Requirements')
table(['ID','Requirement','Implementation evidence'],[
('FR01','Register, authenticate and assign roles','JWT endpoints; role-specific screens'),('FR02','Submit and monitor assistance requests','Citizen form, request status and history'),('FR03','Human-reviewable AI prioritisation','Assessment and review endpoints'),('FR04','Manage crises and operational areas','Crisis API and service-area entity'),('FR05','Constrained stock reservation','Resource strategy and SQL transaction'),('FR06','Shelter allocation within capacity','Capacity and accessibility checks'),('FR07','Verified volunteer assignments','Availability, skills and task states'),('FR08','Donation tracking','Campaigns and recorded pledges'),('FR09','Notifications and map integration','In-app/outbox, SMTP and OSM geocoding'),('FR10','Operational dashboards and audit','KPIs and actor/timestamp events')])
sub('2.2','Non-Functional Requirements')
table(['Quality','Design response'],[('Security','JWT, password hashing, RBAC, rate limiting and input validation'),('Integrity','Serializable transactions, status checks and unique event keys'),('Reliability','External-service timeout/fallback; durable SQL outbox'),('Maintainability','Modular monolith, dependency injection and repository boundary'),('Usability','Responsive React screens and meaningful error feedback'),('Traceability','Request histories, AI decisions, timestamps and audit events'),('Performance','Indexed query paths and bounded list queries; load testing outstanding')])
sub('2.3','User Roles');paragraph('Actors were mapped to affected persons, emergency coordinators, case managers, resource/logistics managers, shelter administrators, volunteer coordinators, volunteers, donors/funding officers, partner officers and system administrators. Citizen access to cases was restricted by ownership; sensitive staff and administrative endpoints enforce roles on the server. Full organisation-level isolation would require additional production controls.')
sub('2.4','Use Case Diagram');fig('02_use_cases.png','Figure 2. Actor–use-case relationships in the implemented workflow.',6.2)
sub('2.5','Important Use Cases');paragraph('UC1—Submit request: an authenticated resident selected an active crisis, category, urgency, quantity and coordinates; validation and a case-history event were committed. UC2—Review AI: an authorised coordinator requested an assessment and approved, modified, rejected or deferred the pending recommendation. UC3—Allocate resources: staff selected an eligible case and compatible stock, then completed an atomic reservation or received a conflict. UC4—Shelter placement: capacity, suitability and duplicate placement were checked before occupancy changed. UC5—Volunteer task: a verified, skilled, available volunteer accepted and completed an assignment.')

D.add_heading('Chapter 3 – System Architecture',1)
sub('3.1','Architecture Overview');paragraph('A modular monolith was chosen because the scope required consistent transactions across cases, inventories, shelters and outbox events, while a small student team needed manageable local deployment. Domain entities, contracts, data access and services were separated within one ASP.NET Core application. This allowed internal module boundaries without the operational overhead of multiple independently deployed services.')
sub('3.2','Architecture Diagram');fig('01_architecture.png','Figure 1. Browser, API modules, SQL Server and external integrations.',6.3)
sub('3.3','Frontend Architecture');paragraph('React components implement a sidebar-based role-aware workspace with public-facing assistance screens, staff dashboards, resource and shelter management, volunteer assignments, donations, maps, notifications and governance. A shared API client centralises JWT requests and error handling. React state manages form and page-level interactions.')
sub('3.4','Backend Architecture');paragraph('ASP.NET minimal APIs organise HTTP workflows by domain. Identity logic uses ASP.NET password hashing and JWT middleware. Dependency-injected case repositories, AI services, match strategies and an outbox worker isolate important concerns. Authorisation is repeated at resource and workflow boundaries, including case ownership and approval-state checks.')
sub('3.5','Database Architecture');paragraph('EF Core maps Users, RoleDefinitions, PermissionDefinitions, RolePermissions, Organisations, Crises, Requests, RequestHistories, AiRecommendations, Resources, ResourceAllocations, Shelters, ShelterAllocations, Volunteers, VolunteerTasks, Campaigns, Donations, Notifications, Outbox and Audits. Restrictive foreign keys prevent accidental historical cascade deletion. Indexes support case queries and unique email/event keys. Critical stock and shelter updates use serialisable SQL transactions.')
sub('3.6','External Services');paragraph('OpenStreetMap tiles display locations, Nominatim supports optional geocoding, a chat-completions-compatible AI endpoint can be enabled, and SMTP can dispatch email. Each remote dependency is optional or has a documented fallback. Without SMTP, in-app messages remain visible. External AI failure returns a transparent heuristic recommendation. Public map endpoints require internet and provider-policy compliance.')
sub('3.7','Deployment Diagram');fig('06_deployment.png','Figure 6. Containerised SQL Server/API and local React development client.',5.9)

D.add_heading('Chapter 4 – System Design',1)
sub('4.1','Class Diagram');fig('03_class.png','Figure 3. Key domain classes and navigational relationships.',6.2)
sub('4.2','Database Design');fig('04_er.png','Figure 4. Principal database tables, key relationships and event records.',6.0)
paragraph('The assistance request is the central operational record. RequestHistories stores chronological workflow changes while AiRecommendations retains the original suggestion and human outcome. ResourceAllocations and ShelterAllocations record approved fulfilment decisions independently from inventory and occupancy totals. The Outbox persists notification events with unique event keys. AuditEvents retains actor, entity, action, timestamp and structured details.')
sub('4.3','API Design')
table(['Method/path','Purpose','Authorisation'],[('POST /api/auth/login','Issue JWT after password validation','Public'),('POST /api/requests','Create assistance case','Signed in'),('POST /api/requests/{id}/assess','Generate recommendation','Coordinator / Case Manager / Admin'),('POST /api/requests/{id}/review','Record human approval or override','Coordinator / Case Manager / Admin'),('POST /api/resources/allocate','Reserve aid with constraints','Logistics / Coordinator / Admin'),('POST /api/shelters/allocate','Commit capacity-safe placement','Shelter Admin / Coordinator / Admin'),('GET /api/audit','Inspect decision trail','Coordinator / Admin')])
sub('4.4','Sequence Diagrams')
for fn,caption in [('07_submit_request.png','Figure 7. Citizen request submission and history creation.'),('08_ai_human_review.png','Figure 8. AI recommendation, fallback and coordinator review.'),('09_resource_shortage.png','Figure 9. Transactional allocation, shortage conflict and outbox.'),('10_volunteer_assignment.png','Figure 10. Volunteer verification and task lifecycle.')]: fig(fn,caption,6.25)
sub('4.5','Component Diagram');fig('05_component.png','Figure 5. Application components and integration boundaries.',5.95)

D.add_heading('Chapter 5 – Design Patterns and Architectural Decisions',1)
sub('5.1','Design Patterns Used');paragraph('The repository pattern was used in IRequestRepository/EfRequestRepository to separate case reads from endpoint code. ASP.NET Core dependency injection registered services, HTTP clients and the repository. IResourceMatchStrategy allowed full-quantity matching or partial shortage-aware matching; ResourceMatchStrategyFactory selected the policy. The notification outbox provided an asynchronous event-processing pattern with a background worker and idempotency keys.')
sub('5.2','Why Patterns Were Selected');paragraph('The repository enabled testable persistence boundaries. Dependency injection reduced coupling and simplified replacement of the AI provider. Strategy/factory made different matching policies explicit rather than hiding them in controllers. The outbox avoided making email availability a prerequisite for completing an approved humanitarian operation.')
sub('5.3','Architectural Decisions');paragraph('The modular-monolith structure reduced deployment and transaction complexity. SQL Server was retained as the relational system of record. Human approvals, role checks, strict quantity validation, serialisable allocation transactions and recorded overrides were preferred over autonomous action. Local demo seeding used EnsureCreated; production schema evolution should use formal EF Core migrations.')
sub('5.4','Alternatives Considered');paragraph('Microservices would improve independent scaling but introduce distributed transactions and operations overhead. A document database would simplify flexible payloads but weaken the relational integrity emphasis. Fully autonomous AI triage was rejected because uncertain humanitarian decisions require accountable review. A live payment gateway was not selected because payment processing was optional and security obligations would exceed prototype scope.')

D.add_heading('Chapter 6 – AI Decision Support',1)
sub('6.1','Role of AI');paragraph('AI outputs were used only as advisory inputs to an authorised decision-maker. The core implementation exposes request classification, urgency scoring, basic duplicate flags and constrained matching recommendations. It does not claim validated predictive medical triage or clinical reliability.')
sub('6.2','AI Features');paragraph('A local transparent heuristic calculated priority from urgency, reported medical emergency, household size and accessibility. Keyword features inferred limited request categories when the submitted category was Other. Potential duplicates were highlighted using similar location/text, without deletion. Alternative resource matching was ranked by suitable category, sufficient quantity, accessibility and approximate distance. An optional external AI request could supply category, priority, confidence and rationale; returned values were validated and bounded.')
sub('6.3','AI Workflow');paragraph('An authorised coordinator initiated assessment. The service used a configured external model if available, otherwise a deterministic fallback. A recommendation row retained its model identity, score and rationale. The request moved to PendingReview. No resource allocation proceeded before a later human approval created the Verified status.')
sub('6.4','Human-in-the-Loop');paragraph('Approving, modifying, rejecting and deferring were restricted to qualified roles. A modification required a revised priority and reason; rejection required a reason. The original recommendation remained unchanged and review metadata recorded final priority, user identity and time. Human decisions were also written to the audit and case-history streams.')
sub('6.5','AI Limitations');paragraph('The fallback confidence value was a user-interface indicator and not calibrated empirical probability. Keyword categorisation was English-biased; multilingual response generation was not deployed as a verified feature. External requests could still contain identifying details even after basic email/phone stripping, so privacy review and consent are required before enabling an external model. Automated forecasts and anomaly detection were not demonstrated as validated capabilities.')

D.add_heading('Chapter 7 – Implementation',1)
sub('7.1','Technology Stack');paragraph('The source uses .NET 8/C#, ASP.NET Core minimal APIs, Entity Framework Core, SQL Server 2022, React 18, Vite, HTML/CSS, Leaflet/OpenStreetMap, JWT middleware, xUnit and Swagger/OpenAPI. Docker Compose supplies repeatable SQL Server and API configuration.')
sub('7.2','Frontend Implementation');paragraph('The responsive client contains authentication, dashboards, request forms with map pin selection, AI case reviews, inventory and shelter allocations, volunteer tasks, donation pledges, notifications, map views and audit tables. Shared inputs, status pills and error panels promote consistency. The development proxy avoids hard-coded production API hosts.')
sub('7.3','Backend Implementation');paragraph('Business operations were grouped by feature and returned conventional HTTP errors for invalid input, forbidden roles and allocation conflicts. Request histories and audit records were created in the same DbContext transaction as their corresponding business changes. An outbox worker polled pending notification events and classified delivery as Sent, Failed or InAppOnly.')
sub('7.4','Database Implementation');paragraph('Code-first tables were generated from the DbContext, with unique event/email indexes and restrictive relationships. Stock and occupancy were updated within serialisable transactions. Supplemental SQL views for operational reporting and verification queries were supplied in the database folder.')
sub('7.5','API Implementation');paragraph('Swagger/OpenAPI documented the routes, and a Postman collection included login token capture, case creation, AI review, allocation and resilience scenarios. Paths were scoped to /api and used consistent JSON response shapes; the React client included session handling and server error feedback.')
sub('7.6','Authentication and Authorisation');paragraph('Passwords were hashed with ASP.NET Identity PasswordHasher, and signed JWTs carried a six-hour expiration and role claims. Server-side role restrictions and citizen ownership checks limited access to cases and administrative functions. Rate limiting protected authentication and geocoding endpoints. For production, browser token storage should be replaced with hardened HttpOnly cookie flows and additional organisation-level controls.')
sub('7.7','External Service Integration');paragraph('The AI HTTP client used a six-second timeout and validated JSON before persisting advice. Nominatim geocoding supported place lookup with manual coordinate fallback. SMTP, when configured, was processed asynchronously; in-app notification records remained available without SMTP. OpenStreetMap tile use requires provider terms and internet connectivity.')

D.add_heading('Chapter 8 – Testing',1)
sub('8.1','Testing Strategy');paragraph('A four-level plan was prepared: isolated logic tests, authenticated API checks, cross-module integration tests and user acceptance demonstrations. Key scenarios exercised priority ordering, invalid input, wrong roles, human overrides, constrained stock, capacity conflicts, unavailable providers and duplicate notification events.')
sub('8.2','Unit Testing');paragraph('xUnit source tests were included for urgency comparison, score bounds, category inference, accessibility/expiry exclusion, matching policies and distance. These test files are supplied for execution using the .NET SDK.')
sub('8.3','API Testing');paragraph('Postman and Swagger examples covered authentication, request creation, assessment, review, allocation and audit. A shortage test expects HTTP 409 and no inventory decrement. Unauthorized access should return 401 or 403 as appropriate.')
sub('8.4','Integration Testing');paragraph('Planned integration scenarios linked the React client, SQL Server data changes, AI fallback, mapping and outbox delivery. A multi-client allocation stress check was specified to validate the serialisable stock reservation process. Database-before/after evidence should be captured during local execution.')
sub('8.5','User Acceptance Testing');paragraph('Acceptance scenarios covered citizen submission, coordinator review, approval/override, logistics and shelter assignment, citizen notification and audit review. The presentation runbook specifies the screen sequence, expected status changes and evidence locations.')
sub('8.6','Test Results')
table(['Area','Prepared evidence','Actual execution'],[('Unit','xUnit project and assertions','NOT RUN here — .NET SDK unavailable'),('API','Swagger and Postman collection','NOT RUN here — API cannot be launched'),('Integration','Cross-module and failure cases','NOT RUN here'),('Acceptance','10-minute demonstrator script','NOT RECORDED')])
paragraph('No passing runtime result is asserted. The construction environment did not have the .NET SDK and could not download frontend npm dependencies. The group must execute and document the tests before reporting final pass rates or submitting evaluation screenshots.')

D.add_heading('Chapter 9 – System Demonstration and Evaluation',1)
sub('9.1','User Interface');paragraph('The frontend source includes the major pages required for a demonstration. Screenshots must be captured from the actual running client after installation, not substituted by UI mock-ups or generated images.')
sub('9.2','Main System Features');paragraph('A repeatable demo was prepared: sign in as citizen and submit a case; sign in as coordinator to produce and review advice; reserve resources as logistics manager; approve shelter placement; verify and assign a volunteer; record a donor pledge; inspect notifications and audit history.')
sub('9.3','AI Functionality');paragraph('The case-detail view was designed to display recommended priority, confidence, model identifier, rationale and possible duplicate flags. Staff can select Approved, Modified, Rejected or Deferred and see the resulting history. The original AI record and override fields should be visible in SQL evidence.')
sub('9.4','API Testing Evidence');paragraph('Swagger is available at /swagger in development. Postman examples in postman/CrisisAid.postman_collection.json are prepared for evidence capture. Real request/response screenshots are pending group execution.')
sub('9.5','Database Evidence');paragraph('SQL verification queries are included for cases, recommendations, allocations, stock, shelter occupancy, outbox and audit events. Real SQL Server Management Studio screenshots are pending group execution.')
sub('9.6','Evaluation');paragraph('The delivered source addresses the principal architecture and workflow requirements on paper and in code, including mandatory role-based flows and constrained allocations. However, empirical achievement cannot be confirmed without compiling, launching and testing it. Production gaps include privacy assessment, tested multilingual communication, hardened tenant isolation, robust evidence uploads and real-world operational validation.')
table(['Evidence item','Status / location'],[('Web portal/screenshots','Source present; genuine capture PENDING'),('AI approval/override evidence','Workflow code present; runtime capture PENDING'),('Swagger/Postman evidence','Collection present; run screenshot PENDING'),('SQL Server evidence','Queries supplied; actual screenshot PENDING'),('Notification and map integration','Code present; provider testing PENDING'),('Git and individual contribution','Real student evidence REQUIRED')])

D.add_heading('Chapter 10 – Individual Contributions and Source Links',1)
sub('10.1','Individual Contributions')
paragraph('The following entries MUST be replaced by factual member work, with name, index number, specific features, technologies, Git commits, encountered risks and mitigation. Generated text cannot substantiate an individual contribution.')
for i in range(1,7):
 paragraph(f'Member {i} — [NAME, INDEX]: [ACTUAL responsibilities, implemented screens/API/SQL/services, tests, key commits/PR IDs, design decision and resolved risk].')
sub('10.2','GitHub Commit History and Repository');paragraph('Repository: [GITHUB_URL]. Add actual GitHub commit history links, PR references and a contribution summary for each member. Do not fabricate commits or substitute this template for real version-control evidence.')
sub('10.3','Project Source Code and Video Links');paragraph('NSBM OneDrive source ZIP (grant evaluator access): [SOURCE_ONEDRIVE_URL]. Demonstration recording (grant evaluator access): [VIDEO_ONEDRIVE_URL]. The report guideline warns that missing or inaccessible source code links can result in zero marks. Links must be inserted by the group after uploading genuine files.')
D.add_heading('References',1)
for ref in [
 'NSBM Green University. (2026). SE205.3 Software Architecture Group Coursework: AI-Powered Humanitarian Crisis Management and Aid Coordination Platform. (User-supplied module brief).',
 'NSBM Green University. (2026). Final Report Structure and Report Style. (User-supplied instructions).',
 'NSBM Green University. (2026). Software Architecture Presentation Recording Evaluation & Submission Guidelines. (User-supplied instructions).',
 'Microsoft. (n.d.). ASP.NET Core documentation. https://learn.microsoft.com/aspnet/core/',
 'Microsoft. (n.d.). Entity Framework Core documentation. https://learn.microsoft.com/ef/core/',
 'OpenStreetMap contributors. (n.d.). OpenStreetMap. https://www.openstreetmap.org/copyright',
 'OWASP. (n.d.). OWASP API Security Top 10. https://owasp.org/API-Security/'
 ]:paragraph(ref)
D.add_heading('Appendix A – Source and Setup',1)
paragraph('See README.md for Docker Compose, frontend startup, API paths, test credentials and known limitations; docs/TEST_PLAN.md for the case matrix; diagrams/ for editable graph/sequence sources; database/ for SQL validation queries; and postman/ for the API collection.')
D.add_heading('Appendix B – Demonstration Evidence Instructions',1)
paragraph('Before final submission, insert genuine screenshots of the React dashboard, citizen request form, AI decision/override screen, resources, shelter capacities, volunteer task, Swagger/Postman authentication and SQL Server recommendation/audit/outbox tables. Cite figure numbers and remove all PENDING labels only once proof has been captured.')

OUT.mkdir(parents=True,exist_ok=True)
path=OUT/'Group_XX_Final_Report.docx';D.save(path)
text=' '.join(p.text for p in D.paragraphs if p.text and not p.style.name.startswith('Title'))
print('Report path:',path,'paragraph words',len(text.split()),'all table words',sum(len(c.text.split()) for T in D.tables for row in T.rows for c in row.cells))

# Separate presentation recording Word submission document as required by presentation instructions.
R=Document();s=R.styles['Normal'];s.font.name='Aptos';s.font.size=Pt(11)
R.add_heading('SE205.3 Presentation Recording Submission',0)
R.add_paragraph('Group number: [GROUP_NUMBER]')
R.add_paragraph('Six group members (replace with actual names and index numbers):')
for i in range(1,7):R.add_paragraph(f'{i}. [NAME] — [INDEX NUMBER]')
R.add_heading('Zoom / Teams demonstration recording',1)
R.add_paragraph('NSBM OneDrive video link: [VIDEO_ONEDRIVE_URL]')
R.add_paragraph('Recording duration: [TEN MINUTES]')
R.add_paragraph('Access for all evaluators confirmed: [YES/NO — verify after upload]')
R.add_heading('Project source',1)
R.add_paragraph('NSBM OneDrive source code ZIP: [SOURCE_ONEDRIVE_URL]')
R.add_paragraph('GitHub repository: [GITHUB_URL]')
R.add_paragraph('This template does not contain a recording or valid external URLs. Upload the actual video/source and replace all placeholders before submitting to NLearn.')
R.save(OUT/'Presentation_Video_Link_Submission.docx')
