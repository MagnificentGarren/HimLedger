import React, { useState } from 'react';
import { ArrowDownRight, ArrowRight, ArrowUpRight, BadgeCheck, Boxes, BriefcaseBusiness, ChartNoAxesCombined, Check, ChevronRight, CircleDollarSign, Cloud, Code2, Database, FileCheck2, Fingerprint, GitBranch, LockKeyhole, Network, ReceiptText, ShieldCheck, Timer, UsersRound, WalletCards } from 'lucide-react';
import { Link, NavLink } from 'react-router-dom';

export const BrandMark: React.FC<{ size?: number }> = ({ size = 25 }) => (
  <svg aria-hidden="true" width={size} height={size} viewBox="0 0 28 28" fill="none">
    <path d="M15.8 4.6c4 .5 7.4 2.2 10.1 5.2-2.2.1-4.2.8-6 2.2 1.5 1.8 2.6 3.8 3.1 6.2-2-.9-4.2-1.4-6.6-1.2l-2.1-3.2 1.5-9.2Z" fill="#B4FFD9" />
    <path d="M5 4.5h3.2v7h8.1V5.4h3.2v5.1h5.1l-8.3 6.2v6.8h-3.2v-8.8H8.2v8.8H5V4.5Z" fill="currentColor" />
  </svg>
);

const navItems = [
  { to: '/', label: 'Home' },
  { to: '/features', label: 'Features' },
  { to: '/pricing', label: 'Pricing' },
];

const TopNav: React.FC = () => (
  <header className="public-nav">
    <Link className="public-brand" to="/" aria-label="HimLedger home">
      <span className="public-brand-mark"><BrandMark /></span>
      <span>HimLedger</span>
    </Link>
    <nav className="public-nav-links" aria-label="Main navigation">
      {navItems.map((item) => (
        <NavLink key={item.label} to={item.to} end={item.to === '/'} className={({ isActive }) => isActive ? 'public-nav-link is-active' : 'public-nav-link'}>
          {item.label}
        </NavLink>
      ))}
    </nav>
    <div className="public-nav-actions">
      <Link className="public-signin" to="/login">Sign in</Link>
      <Link className="public-launch" to="/dashboard">Launch app <ArrowUpRight size={15} aria-hidden="true" /></Link>
    </div>
  </header>
);

const Footer: React.FC = () => (
  <footer className="public-footer" id="company">
    <div className="public-footer-grid">
      <div className="public-footer-brand">
        <Link className="public-brand" to="/">
          <span className="public-brand-mark"><BrandMark /></span>
          <span>HimLedger</span>
        </Link>
        <p>Clear claim workflows, budget visibility, and accountable decisions.</p>
        <small>© {new Date().getFullYear()} HimLedger. All rights reserved.</small>
      </div>
      <div className="public-footer-column">
        <h2>Product</h2>
        <Link to="/features">Features</Link>
        <Link to="/#workflow">Expense workflows</Link>
        <Link to="/features#roles">Budget allocation</Link>
        <Link to="/features#security">Security approach</Link>
      </div>
      <div className="public-footer-column">
        <h2>Engineering</h2>
        <Link to="/about#stack">Architecture overview</Link>
        <Link to="/about#integrations">Integrations</Link>
      </div>
      <div className="public-footer-column">
        <h2>Support</h2>
        <Link to="/pricing">Pricing &amp; access</Link>
        <a href="mailto:support@himledger.com">Contact support</a>
      </div>
    </div>
    <div className="public-footer-bottom"><span>Built for teams that move capital with care.</span><span>Designed for clarity. Accounted for, always.</span></div>
  </footer>
);

const PublicLayout: React.FC<{ children: React.ReactNode }> = ({ children }) => (
  <div className="public-site">
    <TopNav />
    {children}
    <Footer />
  </div>
);

const PageIntro: React.FC<{ tag: string; title: React.ReactNode; text: string }> = ({ tag, title, text }) => (
  <div className="public-page-intro">
    <span className="public-kicker"><span />{tag}</span>
    <h1>{title}</h1>
    <p>{text}</p>
  </div>
);

const steps = [
  { number: '01', icon: ReceiptText, title: 'Employee logs a claim', text: 'Add the expense, choose a category, and provide the relevant details.' },
  { number: '02', icon: ChartNoAxesCombined, title: 'Budget check runs', text: 'Available departmental funds are checked before review.' },
  { number: '03', icon: BadgeCheck, title: 'Manager approves', text: 'The decision, timestamp, and reviewer note join the audit trail.' },
];

export const HomePage: React.FC = () => (
  <PublicLayout>
    <main>
      <section className="public-hero">
        <div className="public-hero-copy">
          <span className="public-kicker"><span />FINANCIAL CONTROL, IN MOTION</span>
          <h1>Corporate expense operations, <em>powered by precision.</em></h1>
          <p>A full-stack financial control portal for logistics, freight, and modern enterprise teams. Monitor budgets from submission to final audit.</p>
          <div className="public-hero-actions">
            <Link className="public-button-primary" to="/login">Access workspace <ArrowRight size={17} /></Link>
            <Link className="public-button-secondary" to="/features#roles">Explore workflows <ArrowDownRight size={16} /></Link>
          </div>
          <div className="public-hero-proof"><span className="proof-icon"><ShieldCheck size={15} /></span>          <span>Role-aware access</span><i /> <span>Recorded decisions</span></div>
        </div>
        <div className="hero-visual" aria-label="HimLedger dashboard preview">
          <div className="hero-orbit hero-orbit-one" />
          <div className="hero-orbit hero-orbit-two" />
          <div className="preview-window">
            <div className="preview-topbar"><div className="preview-title"><span className="preview-mark"><BrandMark size={14} /></span>HimLedger <span className="preview-separator">/</span> Overview</div><span className="preview-period">SAMPLE DATA <ChevronRight size={12} /></span></div>
            <div className="preview-content">
              <div className="preview-greeting"><div><span>DEMO WORKSPACE</span><strong>Spend overview</strong></div><button aria-label="More options">•••</button></div>
              <div className="preview-total"><div><span>Approved spend</span><strong>R 10 000,00</strong></div><span className="preview-trend"><ArrowUpRight size={13} /> 8.4%</span></div>
              <div className="preview-chart" aria-label="Spend trend chart">
                <div className="chart-y-labels"><span>12k</span><span>8k</span><span>4k</span><span>0</span></div>
                <div className="chart-field"><div className="chart-gridline" /><div className="chart-gridline" /><div className="chart-gridline" /><svg viewBox="0 0 480 116" preserveAspectRatio="none" aria-hidden="true"><defs><linearGradient id="spendFill" x1="0" x2="0" y1="0" y2="1"><stop offset="0" stopColor="#34d399" stopOpacity=".24" /><stop offset="1" stopColor="#34d399" stopOpacity="0" /></linearGradient></defs><path d="M0 98 C35 91 39 69 80 76 S123 83 158 62 S196 70 233 51 S281 60 313 39 S355 48 388 31 S445 39 480 12 V116 H0Z" fill="url(#spendFill)" /><path d="M0 98 C35 91 39 69 80 76 S123 83 158 62 S196 70 233 51 S281 60 313 39 S355 48 388 31 S445 39 480 12" fill="none" stroke="#34d399" strokeWidth="2.5" vectorEffect="non-scaling-stroke" /></svg><div className="chart-x-labels"><span>JUL 01</span><span>JUL 15</span><span>AUG 01</span><span>AUG 15</span><span>SEP 01</span></div></div>
              </div>
              <div className="preview-bottom"><div className="preview-budget"><span>Quarterly budget</span><strong>68% <small>used</small></strong><div className="preview-progress"><i /></div><small>R 24 800 remaining</small></div><div className="preview-approval"><span>Recent claim</span><div><span className="preview-avatar">JM</span><div><strong>Freight fuel</strong><small>J. Mokoena · Today</small></div></div><span className="preview-status"><Check size={11} /> Approved</span></div></div>
            </div>
          </div>
          <div className="preview-float"><span><Check size={13} /></span><div><strong>Approval recorded</strong><small>Audit trail updated</small></div></div>
          <span className="visual-caption">ONE WORKSPACE. EVERY RAND ACCOUNTED FOR.</span>
        </div>
      </section>

      <section className="public-metrics" aria-label="Platform highlights">
        <div><strong>Clear</strong><span>Role-aware workspaces <i>Employee, manager, finance, admin</i></span></div>
        <div><strong>Quarterly</strong><span>Department budgets <i>Allocation and spending visibility</i></span></div>
        <div><strong>Recorded</strong><span>Claim decisions <i>History and reviewer comments</i></span></div>
        <div><strong>Connected</strong><span>REST API <i>For configured deployments</i></span></div>
      </section>

      <section className="public-section public-pillars" id="features">
        <div className="section-lead"><span className="public-kicker"><span />CONTROL AT EVERY STEP</span><h2>Spend decisions,<br /><em>made visible.</em></h2></div>
        <div className="pillar-grid">
          <article className="pillar-item"><span className="pillar-number">01</span><span className="pillar-icon"><CircleDollarSign size={19} /></span><h3>Quarterly budget visibility</h3><p>See department allocations and committed or approved spending as claims move through review.</p><Link to="/features">Explore budget controls <ArrowRight size={14} /></Link></article>
          <article className="pillar-item"><span className="pillar-number">02</span><span className="pillar-icon"><Network size={19} /></span><h3>Multi-tier approvals</h3><p>Route claims to the right department manager for a focused review, with decision context in one place.</p><Link to="/features#roles">Explore workflows <ArrowRight size={14} /></Link></article>
          <article className="pillar-item"><span className="pillar-number">03</span><span className="pillar-icon"><FileCheck2 size={19} /></span><h3>Audit-ready telemetry</h3><p>Keep reviewer timestamps and comments alongside each decision.</p><Link to="/about#stack">Explore architecture <ArrowRight size={14} /></Link></article>
        </div>
      </section>

      <section className="workflow-band" id="workflow">
        <div className="workflow-heading"><span className="public-kicker"><span />A CLEAR PATH TO CLOSE</span><h2>From claim to decision.</h2><p>One consistent flow gives every claim context and every decision a home.</p></div>
        <div className="workflow-steps">{steps.map(({ number, icon: Icon, title, text }) => <article className="workflow-step" key={number}><div className="workflow-step-top"><span>{number}</span><Icon size={19} /></div><h3>{title}</h3><p>{text}</p></article>)}</div>
      </section>
      <section className="public-cta-strip"><div><span className="public-kicker"><span />READY WHEN YOU ARE</span><h2>Bring every expense into view.</h2></div><Link className="public-button-primary" to="/login">Enter your workspace <ArrowRight size={16} /></Link></section>
    </main>
  </PublicLayout>
);

type RoleKey = 'employee' | 'manager' | 'finance' | 'admin';

const roleContent: Record<RoleKey, { label: string; title: string; description: string; icon: typeof UsersRound; points: string[] }> = {
  employee: { label: 'Employee', title: 'Submit once. Follow every step.', description: 'A focused claim flow gives employees the essentials without losing the detail finance needs.', icon: BriefcaseBusiness, points: ['Enter amount, date, and expense category', 'Add notes about the business purpose', 'Track status through review and approval'] },
  manager: { label: 'Manager', title: 'Review with the full picture.', description: 'Keep the pending queue focused and make decisions with budget and claim context close at hand.', icon: UsersRound, points: ['See pending claims for your department', 'Approve or reject with a recorded comment', 'Review decision history and timestamps'] },
  finance: { label: 'Finance', title: 'Keep budgets and reimbursement in view.', description: 'Review submitted claims, monitor allocations, and take action on approved expenses.', icon: WalletCards, points: ['Review claims and department spending', 'Monitor quarterly allocations and remaining amounts', 'Process claims approved for reimbursement'] },
  admin: { label: 'Admin', title: 'Manage access and workspace setup.', description: 'Keep account access, departments, and workspace roles aligned with the organization.', icon: ShieldCheck, points: ['Provision accounts and assign workspace roles', 'Manage departments and identity links', 'Deactivate access while preserving claim history'] },
};

export const FeaturesPage: React.FC = () => {
  const [activeRole, setActiveRole] = useState<RoleKey>('employee');
  const role = roleContent[activeRole];
  const RoleIcon = role.icon;

  return (
    <PublicLayout>
      <main className="public-inner-page">
        <PageIntro tag="BUILT AROUND THE WORK" title={<>Clear roles.<br /><em>Connected controls.</em></>} text="Expense operations work best when each person sees what they need and each handoff keeps its context." />
        <section className="role-section" id="roles">
          <div className="role-section-heading"><div><span className="public-kicker"><span />ONE SYSTEM, THREE PERSPECTIVES</span><h2>Made for the people<br />behind every decision.</h2></div><p>Choose a role to see how HimLedger keeps everyday expense work moving.</p></div>
          <div className="role-tabs" role="tablist" aria-label="Explore by role">{(Object.keys(roleContent) as RoleKey[]).map((key) => { const TabIcon = roleContent[key].icon; return <button type="button" role="tab" aria-selected={activeRole === key} className={activeRole === key ? 'role-tab is-active' : 'role-tab'} key={key} onClick={() => setActiveRole(key)}><TabIcon size={16} />{roleContent[key].label}</button>; })}</div>
          <div className="role-panel" role="tabpanel"><div className="role-panel-copy"><div className="role-panel-icon"><RoleIcon size={20} /></div><span className="public-kicker">{role.label.toUpperCase()} WORKSPACE</span><h3>{role.title}</h3><p>{role.description}</p></div><ul>{role.points.map((point) => <li key={point}><span><Check size={13} /></span>{point}</li>)}</ul><div className="role-panel-index">HIMLEDGER <span>·</span> {String(Object.keys(roleContent).indexOf(activeRole) + 1).padStart(2, '0')}</div></div>
        </section>
        <section className="security-section" id="security"><div className="security-heading"><span className="security-emblem"><ShieldCheck size={21} /></span><span className="public-kicker"><span />SECURITY &amp; ACCESS</span><h2>Controls you can explain.</h2><p>Authentication, role-aware access, input validation, and an auditable claim history are part of the application workflow.</p></div><div className="security-points"><article><Fingerprint size={18} /><div><h3>Authenticated access</h3><p>API routes require authentication and use role-aware access checks.</p></div></article><article><LockKeyhole size={18} /><div><h3>Validated requests</h3><p>Request validation helps keep inputs predictable and well-formed.</p></div></article><article><Database size={18} /><div><h3>Relational integrity</h3><p>Database constraints and transaction handling protect linked financial records.</p></div></article></div></section>
        <section className="public-cta-strip"><div><span className="public-kicker"><span />SEE THE SYSTEM</span><h2>Follow a claim all the way through.</h2></div><Link className="public-button-primary" to="/about">Explore architecture <ArrowRight size={16} /></Link></section>
      </main>
    </PublicLayout>
  );
};

export const PricingPage: React.FC = () => (
  <PublicLayout>
    <main className="public-inner-page pricing-page">
      <PageIntro tag="PRICING & ACCESS" title={<>Clear access.<br /><em>No made-up rates.</em></>} text="Standard pricing is not published yet. Access and deployment arrangements depend on the organization using HimLedger." />
      <section className="pricing-overview">
        <div className="pricing-overview-copy"><span className="pricing-mark"><CircleDollarSign size={22} /></span><span className="public-kicker"><span />ACCESS FOR ORGANIZATIONS</span><h2>Everything stays connected.</h2><p>Expense submission, department budget visibility, and review workflows come together in one workspace.</p><Link className="public-button-primary" to="/login">Sign in to your workspace <ArrowRight size={16} /></Link></div>
        <div className="pricing-inclusions"><h3>Workspace capabilities</h3><ul><li><span><Check size={13} /></span>Employee, manager, finance, and admin roles</li><li><span><Check size={13} /></span>Department budgets and expense categories</li><li><span><Check size={13} /></span>Approval decisions with recorded history</li><li><span><Check size={13} /></span>REST API available for configured deployments</li></ul><p>Already part of an organization? Ask its HimLedger administrator about access and applicable terms. Looking at HimLedger for your organization? Contact support for current availability and pricing information.</p><a className="public-button-secondary" href="mailto:support@himledger.com">Contact support <ArrowUpRight size={15} /></a></div>
      </section>
      <section className="pricing-note"><ShieldCheck size={17} /><p>Workspace accounts are provisioned by an administrator. HimLedger does not offer open public registration.</p></section>
    </main>
  </PublicLayout>
);

const layers = [
  { name: 'Client', detail: 'React · TypeScript · Vite', icon: Code2, number: '01' },
  { name: 'API', detail: 'ASP.NET Core 8 · REST · JWT', icon: Boxes, number: '02' },
  { name: 'Application', detail: 'Use cases · DTOs · Validation', icon: GitBranch, number: '03' },
  { name: 'Domain & data', detail: 'C# · EF Core · SQL Server', icon: Database, number: '04' },
];

export const ArchitecturePage: React.FC = () => (
  <PublicLayout>
    <main className="public-inner-page architecture-page">
      <PageIntro tag="ENGINEERED FOR ACCOUNTABILITY" title={<>A clear blueprint<br /><em>for financial control.</em></>} text="A layered application keeps user workflows, business rules, and data access distinct, with a practical path to cloud deployment." />
      <section className="architecture-stack" id="stack"><div className="architecture-heading"><div><span className="public-kicker"><span />SOFTWARE BLUEPRINT</span><h2>Four layers. One ledger.</h2></div><span className="architecture-tag"><span />CLEAN ARCHITECTURE</span></div><div className="layer-list">{layers.map(({ name, detail, icon: Icon, number }) => <article className="layer-row" key={number}><span className="layer-number">{number}</span><span className="layer-icon"><Icon size={18} /></span><div className="layer-label"><h3>{name}</h3><p>{detail}</p></div><ArrowUpRight className="layer-arrow" size={16} /></article>)}</div></section>
      <section className="tech-grid"><article className="tech-panel"><span className="tech-panel-icon"><Cloud size={18} /></span><span className="public-kicker">CLOUD &amp; DELIVERY</span><h2>Deploy with confidence.</h2><p>Designed around Microsoft Azure services, with a deployment path that can grow alongside the organization.</p><div className="tech-tags"><span>Azure App Services</span><span>Azure SQL</span><span>GitHub Actions</span></div></article><article className="tech-panel"><span className="tech-panel-icon"><Database size={18} /></span><span className="public-kicker">DATA MODEL</span><h2>Relationships with purpose.</h2><p>Users, departments, budgets, categories, expenses, and approval logs form the core of an auditable relational model.</p><div className="schema-preview"><span>USER</span><i /><span>EXPENSE</span><i /><span>APPROVAL LOG</span><div className="schema-second"><span>DEPARTMENT</span><i /><span>BUDGET</span><i /><span>CATEGORY</span></div></div></article></section>
      <section className="api-section" id="integrations"><div className="api-intro"><span className="public-kicker"><span />INTEGRATION SURFACE</span><h2>REST endpoints,<br /><em>clearly organized.</em></h2><p>The API supports claims, budgets, and authentication. Documentation and endpoint availability depend on the organization's deployment.</p></div><div className="api-preview"><div className="api-preview-head"><span><i /> API ROUTES</span><span>HIMLEDGER · V1</span></div><div className="api-route"><span className="api-method">POST</span><code>/api/Auth/login</code><span className="api-route-label">Authentication</span></div><div className="api-route"><span className="api-method api-method-get">GET</span><code>/api/Expenses</code><span className="api-route-label">Expense claims</span></div><div className="api-route"><span className="api-method api-method-get">GET</span><code>/api/Budgets</code><span className="api-route-label">Budget overview</span></div><div className="api-route"><span className="api-method">POST</span><code>/api/Expenses</code><span className="api-route-label">Submit a claim</span></div><div className="api-preview-foot"><span><Timer size={13} /> ASP.NET CORE</span><span>JSON · REST</span></div></div></section>
      <section className="public-cta-strip" id="pricing"><div><span className="public-kicker"><span />HIMLEDGER WORKSPACE</span><h2>See financial operations in context.</h2></div><Link className="public-button-primary" to="/login">Launch workspace <ArrowRight size={16} /></Link></section>
    </main>
  </PublicLayout>
);