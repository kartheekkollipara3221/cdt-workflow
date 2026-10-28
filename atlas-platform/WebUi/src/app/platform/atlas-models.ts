// COPY to the target: WebUi/Source/App/platform/. The server DTOs (PascalCase, as the API sends them).
// Only the properties that the platform form and dashboard use. All new server fields are optional.

export interface CPKeyValue {
  Label?: string | null;
  Value?: string | null;
  Data?: Record<string, string> | null;
}

export interface CPValidator {
  Required?: boolean;
  Message?: string | null;
  Pattern?: string | null;
  MinLength?: number | null;
  MaxLength?: number | null;
  Integer?: boolean;
  Min?: number | null;
  Max?: number | null;
}

export interface CPEffectCondition {
  Equals?: string | null;
  NotEquals?: string | null;
  In?: string[] | null;
  HasValue?: boolean | null;
}

export interface CPEffect {
  When?: CPEffectCondition | null;
  Show?: string[] | null;
  Hide?: string[] | null;
  Require?: string[] | null;
  Unrequire?: string[] | null;
  Enable?: string[] | null;
  Disable?: string[] | null;
  Set?: Record<string, string> | null;
  Clear?: string[] | null;
  SetFromOption?: Record<string, string> | null;
}

export interface CPGridColumn {
  headerName?: string | null;
  width?: number;
  field?: string | null;
  cellRendererParams?: { disabled?: boolean } | null;
}

export interface CPGrid {
  Columns?: CPGridColumn[] | null;
  Data?: Record<string, string>[] | null;
  BindMode?: string | null;
}

export interface CPApplicationControl {
  Name: string;
  Label: string;
  Type: string;
  Field?: string | null;
  Value?: string | null;
  Options?: CPKeyValue[] | null;
  Disabled?: boolean;
  Visible?: boolean;
  PlaceHolder?: string | null;
  DivClass?: string | null;
  Class?: string | null;
  Row?: number | null;
  Multiple?: boolean;
  Validator?: CPValidator | null;
  Grid?: CPGrid | null;
  ChildControls?: CPApplicationControl[] | null;
  Effects?: CPEffect[] | null;
}

export interface CPButtonAction {
  Type: string;
}

export interface CPButtonTarget {
  AppCode: string;
  Id: number;
}

export interface CPButton {
  Name?: string | null;
  Label?: string | null;
  Disabled?: boolean;
  Visible?: boolean;
  Action?: CPButtonAction | null;
  Target?: CPButtonTarget | null;
}

export interface CPUserPermissions {
  CanCreate: boolean;
  CanEdit: boolean;
  CanDelete: boolean;
  CanViewHistory: boolean;
}

export interface CPApplicationSection {
  Name?: string | null;
  /** Shown under the name (cp-atlas-sectionmaster.html center-container). */
  Description?: string | null;
  HeaderName?: string | null;
  /** The class of the form (cp-atlas-sectionmaster.ts formClass). */
  StepClass?: string | null;
  AppCode?: string | null;
  Version?: string | null;
  UserPermissions?: CPUserPermissions | null;
  Computed?: Record<string, string> | null;
  Controls?: CPApplicationControl[] | null;
  Buttons?: CPButton[] | null;
}

export interface CPPageOutput {
  Name: string;
  Value: string | null;
}

export interface CPPageError {
  Field: string;
  Code: string;
  Message: string;
}

export interface CPAtlasPage {
  AppCode?: string | null;
  ID: number;
  Version?: string | null;
  Outputs?: CPPageOutput[] | null;
  Errors?: CPPageError[] | null;
}

export interface CPTabColumn {
  Name: string;
  Label?: string | null;
  Type?: string | null;
  Format?: string | null;
  Hidden?: boolean;
  Width?: number | null;
  Link?: string | null;
  Sum?: boolean;
}

export interface CPMenu {
  Name?: string | null;
  Label?: string | null;
  Visible?: boolean;
}

export interface CPAtlasTab {
  ID?: string | null;
  Code?: string | null;
  Label?: string | null;
  Visible?: boolean;
  KeyValues?: Record<string, unknown>[] | null;
  Columns?: CPTabColumn[] | null;
  Menus?: CPMenu[] | null;
}

export interface CPAtlasDashboard {
  AppCode?: string | null;
  Name?: string | null;
  Tabs?: CPAtlasTab[] | null;
  Buttons?: CPButton[] | null;
}

export interface CPAuditChange {
  Name: string;
  Label?: string | null;
  Old?: string | null;
  Value?: string | null;
}

export interface CPAuditLog {
  AppCode: string;
  RefID: number;
  CreatedBy: string;
  CreatedDate: string;
  CreatedDateUtc?: string | null;
  Event?: string | null;
  LogMessage?: unknown;
}

/** A row of v1/Application. FormType and HasDashboard are new optional fields (spec 07, 4.6). */
export interface CPApplicationRow {
  ID: number;
  Name: string;
  Code: string;
  Role?: string | null;
  FormType?: string | null;
  HasDashboard?: boolean | null;
}
