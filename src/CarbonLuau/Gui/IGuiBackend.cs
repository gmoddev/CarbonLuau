using System;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        internal enum GuiBackendOperationKind { Replace = 1, Update = 2, Destroy = 3, Scroll = 4 }
        internal enum GuiBackendResultCode { Accepted = 0, TargetUnavailable = 1, SendFailed = 2 }

        internal sealed class GuiBackendResult
        {
            internal readonly GuiBackendResultCode Code; internal readonly string Diagnostic;
            internal bool Accepted { get { return Code == GuiBackendResultCode.Accepted; } }
            private GuiBackendResult(GuiBackendResultCode Code, string Diagnostic)
            { this.Code = Code; this.Diagnostic = Diagnostic ?? ""; }
            internal static GuiBackendResult Success() { return new GuiBackendResult(GuiBackendResultCode.Accepted, ""); }
            internal static GuiBackendResult Failure(GuiBackendResultCode Code, string Diagnostic)
            {
                if (Code == GuiBackendResultCode.Accepted) throw new InvalidOperationException("failure result requires a failure code");
                if (String.IsNullOrEmpty(Diagnostic) || Diagnostic.Length > 256 || Diagnostic.IndexOf('\0') >= 0)
                    throw new InvalidOperationException("backend failure diagnostic is invalid");
                return new GuiBackendResult(Code, Diagnostic);
            }
        }

        internal interface IGuiBackend
        {
            int MeasureReplace(GuiBackendTarget Target, GuiRenderPlan Plan);
            int MeasureUpdate(GuiBackendTarget Target, GuiRenderPatch Patch);
            int MeasureDestroy(GuiBackendTarget Target);
            int MeasureScroll(GuiBackendTarget Target, GuiScrollEffect Effect);
            GuiBackendResult Replace(GuiBackendTarget Target, GuiRenderPlan Plan);
            GuiBackendResult Update(GuiBackendTarget Target, GuiRenderPatch Patch);
            GuiBackendResult Destroy(GuiBackendTarget Target);
            GuiBackendResult Scroll(GuiBackendTarget Target, GuiScrollEffect Effect);
        }
    }
}
