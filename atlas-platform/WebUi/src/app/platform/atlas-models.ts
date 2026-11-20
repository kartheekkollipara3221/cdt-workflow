// COPY to the target: WebUi/src/app/platform/. The server DTOs (PascalCase, as the API sends them).
// Only the properties that the platform form and dashboard use. All new server fields are optional.

/** An option of a control: label, value and optional extra data. */
export interface CPKeyValue {
  Label?: string | null;
  Value?: string | null;
  Data?: Record<string, string> | null;
}

/** The validation rules of a control. */
export interface CPValidator {
  Required?: boolean;
  Message?: string | null;
  Pattern?: string | null;
  MinLength?: number | null;
  MaxLength?: number | null;
  Integer?: boolean;
  Min?: number | null;
  Max?: number | null;
  Step?: number | null;
  MustBeTrue?: boolean;
}

/** The condition on the value of a control that starts an effect. */
export interface CPEffectCondition {
  Equals?: string | null;
  NotEquals?: string | null;
  In?: string[] | null;
  HasValue?: boolean | null;
}

/** An effect of a control: the changes to other controls when the condition is true. */
export interface CPEffect {
  /**
   * When the effect runs. Null: when the form loads and at each change. "Change": only when the user changes the value.
   *
   * The server does not apply a "Change" effect. It is a help for the user only.
   */
  On?: 'Change' | null;
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

/** An ag-grid column of the JSON (the target sends ag-grid ColDef fields as they are). */
export interface CPGridColumn {
  headerName?: string | null;
  width?: number;
  field?: string | null;
  wrapHeaderText?: boolean;
  autoHeaderHeight?: boolean;
  /** The name of a cell component (NumericEditor, TextEditor, SelectEditor at the target). */
  cellRenderer?: string | null;
  cellRendererParams?: { disabled?: boolean; code?: string; ctrlname?: string; [key: string]: unknown } | null;
  /** Platform: the header shows a date from a date control of the form (not an ag-grid field). */
  HeaderFromDate?: CPHeaderFromDate | null;
}

/** Date + AddDays of the control, in Format (default "EEEE dd-MMM-yyyy", as the target TTD form). */
export interface CPHeaderFromDate {
  Control: string;
  AddDays?: number | null;
  Format?: string | null;
}

/** The grid of a grid control: columns, rows and display options. */
export interface CPGrid {
  Columns?: CPGridColumn[] | null;
  Data?: Record<string, string>[] | null;
  BindMode?: string | null;
  Paginate?: boolean | null;
}

/** A control of a form section. */
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
  /** The stored files of a file picker. */
  Files?: CPAttachment[] | null;
  /** The file types of a file picker, for example ['.pdf']. Null = the platform list. */
  Accept?: string[] | null;
  /** The largest file of a file picker in MB. Null = the platform limit. */
  MaxSizeMb?: number | null;
  /** The largest number of files of a file picker. Null = no limit. */
  MaxFiles?: number | null;
}

/** A stored file of a file picker. The load gives no content and no URL. */
export interface CPAttachment {
  ID?: string | null;
  Name?: string | null;
  /** The control name of the file picker. */
  Type?: string | null;
  Disabled?: boolean;
  SizeBytes?: number | null;
  CreatedBy?: string | null;
  CreatedDate?: string | null;
}

/** The action of a button. */
export interface CPButtonAction {
  Type: string;
}

/** The app and the record that a button opens. */
export interface CPButtonTarget {
  AppCode: string;
  Id: number;
}

/** A button of a form or a dashboard. */
export interface CPButton {
  Name?: string | null;
  Label?: string | null;
  Disabled?: boolean;
  Visible?: boolean;
  Action?: CPButtonAction | null;
  Target?: CPButtonTarget | null;
}

/** The permissions of the current user on the app. */
export interface CPUserPermissions {
  CanCreate: boolean;
  CanEdit: boolean;
  CanDelete: boolean;
  CanViewHistory: boolean;
}

/** A form section of a record: controls, buttons, computed values and permissions. */
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

/** A name and a value of a saved page. */
export interface CPPageOutput {
  Name: string;
  Value: string | null;
}

/** An error of a field that the server sends back on save. */
export interface CPPageError {
  Field: string;
  Code: string;
  Message: string;
}

/** The page that the form saves, and the server answer to a save or a remove. */
export interface CPAtlasPage {
  AppCode?: string | null;
  ID: number;
  Version?: string | null;
  Outputs?: CPPageOutput[] | null;
  Errors?: CPPageError[] | null;
  /** The IDs of the stored files that the save removes. */
  RemovedAttachmentIds?: number[] | null;
}

/** A grid column of a dashboard tab. */
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

/** A menu item of a dashboard tab. */
export interface CPMenu {
  Name?: string | null;
  Label?: string | null;
  Visible?: boolean;
}

/** A tab of a dashboard: columns, rows and menus. */
export interface CPAtlasTab {
  ID?: string | null;
  Code?: string | null;
  Label?: string | null;
  /** Shown above the grid (cp-atlas-dashboard.html). */
  Description?: string | null;
  Visible?: boolean;
  KeyValues?: Record<string, unknown>[] | null;
  Columns?: CPTabColumn[] | null;
  Menus?: CPMenu[] | null;
}

/** The dashboard of an app: tabs and buttons. */
export interface CPAtlasDashboard {
  AppCode?: string | null;
  Name?: string | null;
  Tabs?: CPAtlasTab[] | null;
  Buttons?: CPButton[] | null;
}

/** A changed field of an audit log entry. */
export interface CPAuditChange {
  Name: string;
  Label?: string | null;
  Old?: string | null;
  Value?: string | null;
}

/** A row of v1/GetAuditLog. */
export interface CPAuditLog {
  AppCode: string;
  RefID: number;
  CreatedBy: string;
  CreatedDate: string;
  CreatedDateUtc?: string | null;
  Event?: string | null;
  LogMessage?: unknown;
}

/** A row of v1/Application. FormType and HasDashboard are optional platform fields. */
export interface CPApplicationRow {
  ID: number;
  Name: string;
  Code: string;
  Role?: string | null;
  FormType?: string | null;
  HasDashboard?: boolean | null;
}
