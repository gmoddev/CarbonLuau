using System;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        internal sealed class GuiConfig
        {
            internal int MaxObjectsPerScreen = 128, MaxTreeDepth = 16, MaxChildrenPerObject = 64;
            internal int MaxObjectsPerDomain = 1024, MaxObjectsGlobal = 8192, MaxScreensPerDomain = 32;
            internal int MaxScreensPerPlayerConnection = 16, MaxViewersPerScreen = 256;
            internal int MaxPresentationsPerDomain = 512, MaxPresentationsGlobal = 4096;
            internal int MaxButtonsPerScreen = 64, MaxSignalConnectionsPerButton = 8, MaxGuiSignalConnectionsPerDomain = 256;
            internal int MaxActionTokensPerPresentation = 64, MaxActionTokensPerDomain = 8192, MaxActionTokensGlobal = 32768;
            internal int MaxNameUtf8Bytes = 64, MaxTextUtf8Bytes = 2048, MaxTextUtf8BytesPerScreen = 32 * 1024;
            internal int MaxTrackedDirtyObjectsPerDomain = 512, MaxCloneObjects = 128, MaxCloneDepth = 16;
            internal int MaxProjectedElementsPerScreen, MaxRenderElementsPerOperation = 257, MaxRenderPropertiesPerElement = 16;
            internal int MaxSerializedOperationBytes = 64 * 1024, MaxPresentationSendsPerFlush = 64;
            internal int MaxSerializedBytesPerFlush = 256 * 1024, GuiFlushBudgetMicroseconds = 1000;
            internal int PatchBatchesBeforeFull = 32, MaxPlayerInteractionsPerSecond = 20;
            internal int MaxPlayerInteractionBurst = 20, MaxActionInteractionsPerSecond = 8, MaxActionInteractionBurst = 8;

            internal GuiLimits Validate()
            {
                Positive(MaxObjectsPerScreen, "MaxObjectsPerScreen"); Positive(MaxTreeDepth, "MaxTreeDepth");
                Positive(MaxChildrenPerObject, "MaxChildrenPerObject"); Positive(MaxObjectsPerDomain, "MaxObjectsPerDomain");
                Positive(MaxObjectsGlobal, "MaxObjectsGlobal"); Positive(MaxScreensPerDomain, "MaxScreensPerDomain");
                Positive(MaxScreensPerPlayerConnection, "MaxScreensPerPlayerConnection"); Positive(MaxViewersPerScreen, "MaxViewersPerScreen");
                Positive(MaxPresentationsPerDomain, "MaxPresentationsPerDomain"); Positive(MaxPresentationsGlobal, "MaxPresentationsGlobal");
                Positive(MaxButtonsPerScreen, "MaxButtonsPerScreen"); Positive(MaxSignalConnectionsPerButton, "MaxSignalConnectionsPerButton");
                Positive(MaxGuiSignalConnectionsPerDomain, "MaxGuiSignalConnectionsPerDomain");
                Positive(MaxActionTokensPerPresentation, "MaxActionTokensPerPresentation");
                Positive(MaxActionTokensPerDomain, "MaxActionTokensPerDomain"); Positive(MaxActionTokensGlobal, "MaxActionTokensGlobal");
                Positive(MaxNameUtf8Bytes, "MaxNameUtf8Bytes"); Positive(MaxTextUtf8Bytes, "MaxTextUtf8Bytes");
                Positive(MaxTextUtf8BytesPerScreen, "MaxTextUtf8BytesPerScreen");
                Positive(MaxTrackedDirtyObjectsPerDomain, "MaxTrackedDirtyObjectsPerDomain");
                Positive(MaxCloneObjects, "MaxCloneObjects"); Positive(MaxCloneDepth, "MaxCloneDepth");
                if (MaxProjectedElementsPerScreen < 0) throw new InvalidOperationException("MaxProjectedElementsPerScreen cannot be negative");
                Positive(MaxRenderElementsPerOperation, "MaxRenderElementsPerOperation");
                Positive(MaxRenderPropertiesPerElement, "MaxRenderPropertiesPerElement");
                Positive(MaxSerializedOperationBytes, "MaxSerializedOperationBytes");
                Positive(MaxPresentationSendsPerFlush, "MaxPresentationSendsPerFlush");
                Positive(MaxSerializedBytesPerFlush, "MaxSerializedBytesPerFlush");
                Positive(GuiFlushBudgetMicroseconds, "GuiFlushBudgetMicroseconds"); Positive(PatchBatchesBeforeFull, "PatchBatchesBeforeFull");
                Positive(MaxPlayerInteractionsPerSecond, "MaxPlayerInteractionsPerSecond"); Positive(MaxPlayerInteractionBurst, "MaxPlayerInteractionBurst");
                Positive(MaxActionInteractionsPerSecond, "MaxActionInteractionsPerSecond"); Positive(MaxActionInteractionBurst, "MaxActionInteractionBurst");

                AtMost(MaxObjectsPerScreen, MaxObjectsPerDomain, "screen objects must fit the domain object bound");
                AtMost(MaxObjectsPerDomain, MaxObjectsGlobal, "domain objects must fit the global object bound");
                AtMost(MaxChildrenPerObject, MaxObjectsPerScreen, "children must fit the screen object bound");
                AtMost(MaxScreensPerDomain, MaxObjectsPerDomain, "screens must fit the domain object bound");
                AtMost(MaxPresentationsPerDomain, MaxPresentationsGlobal, "domain presentations must fit the global presentation bound");
                AtMost(MaxViewersPerScreen, MaxPresentationsGlobal, "screen viewers must fit the global presentation bound");
                AtMost(MaxButtonsPerScreen, MaxObjectsPerScreen, "buttons must fit the screen object bound");
                AtMost(MaxCloneObjects, MaxObjectsPerScreen, "clone objects must fit the screen object bound");
                AtMost(MaxCloneDepth, MaxTreeDepth, "clone depth must fit the tree depth bound");
                AtMost(MaxTrackedDirtyObjectsPerDomain, MaxObjectsPerDomain, "tracked dirty objects must fit the domain object bound");
                AtMost(MaxActionTokensPerPresentation, MaxActionTokensPerDomain, "presentation tokens must fit the domain token bound");
                AtMost(MaxActionTokensPerDomain, MaxActionTokensGlobal, "domain tokens must fit the global token bound");
                AtMost(MaxTextUtf8Bytes, MaxTextUtf8BytesPerScreen, "one text value must fit the screen text bound");
                AtMost(MaxSerializedOperationBytes, MaxSerializedBytesPerFlush, "one operation must fit the flush byte bound");
                if (MaxRenderElementsPerOperation < MaxObjectsPerScreen)
                    throw new InvalidOperationException("MaxRenderElementsPerOperation must cover MaxObjectsPerScreen");
                int ProjectionLimit = MaxProjectedElementsPerScreen == 0 ? MaxRenderElementsPerOperation : MaxProjectedElementsPerScreen;
                AtMost(ProjectionLimit, MaxRenderElementsPerOperation, "screen projection must fit one render operation");
                return new GuiLimits(this, ProjectionLimit);
            }

            private static void Positive(int Value, string Name)
            { if (Value <= 0) throw new InvalidOperationException(Name + " must be positive"); }
            private static void AtMost(int Value, int Limit, string Message)
            { if (Value > Limit) throw new InvalidOperationException(Message); }
        }

        internal sealed class GuiLimits
        {
            internal readonly int MaxObjectsPerScreen, MaxTreeDepth, MaxChildrenPerObject;
            internal readonly int MaxObjectsPerDomain, MaxObjectsGlobal, MaxScreensPerDomain;
            internal readonly int MaxScreensPerPlayerConnection, MaxViewersPerScreen;
            internal readonly int MaxPresentationsPerDomain, MaxPresentationsGlobal;
            internal readonly int MaxButtonsPerScreen, MaxSignalConnectionsPerButton, MaxGuiSignalConnectionsPerDomain;
            internal readonly int MaxActionTokensPerPresentation, MaxActionTokensPerDomain, MaxActionTokensGlobal;
            internal readonly int MaxNameUtf8Bytes, MaxTextUtf8Bytes, MaxTextUtf8BytesPerScreen;
            internal readonly int MaxTrackedDirtyObjectsPerDomain, MaxCloneObjects, MaxCloneDepth;
            internal readonly int MaxProjectedElementsPerScreen, MaxRenderElementsPerOperation, MaxRenderPropertiesPerElement;
            internal readonly int MaxSerializedOperationBytes, MaxPresentationSendsPerFlush, MaxSerializedBytesPerFlush;
            internal readonly int GuiFlushBudgetMicroseconds, PatchBatchesBeforeFull;
            internal readonly int MaxPlayerInteractionsPerSecond, MaxPlayerInteractionBurst;
            internal readonly int MaxActionInteractionsPerSecond, MaxActionInteractionBurst;

            internal GuiLimits(GuiConfig Value, int ProjectionLimit)
            {
                MaxObjectsPerScreen = Value.MaxObjectsPerScreen; MaxTreeDepth = Value.MaxTreeDepth;
                MaxChildrenPerObject = Value.MaxChildrenPerObject; MaxObjectsPerDomain = Value.MaxObjectsPerDomain;
                MaxObjectsGlobal = Value.MaxObjectsGlobal; MaxScreensPerDomain = Value.MaxScreensPerDomain;
                MaxScreensPerPlayerConnection = Value.MaxScreensPerPlayerConnection; MaxViewersPerScreen = Value.MaxViewersPerScreen;
                MaxPresentationsPerDomain = Value.MaxPresentationsPerDomain; MaxPresentationsGlobal = Value.MaxPresentationsGlobal;
                MaxButtonsPerScreen = Value.MaxButtonsPerScreen; MaxSignalConnectionsPerButton = Value.MaxSignalConnectionsPerButton;
                MaxGuiSignalConnectionsPerDomain = Value.MaxGuiSignalConnectionsPerDomain;
                MaxActionTokensPerPresentation = Value.MaxActionTokensPerPresentation; MaxActionTokensPerDomain = Value.MaxActionTokensPerDomain;
                MaxActionTokensGlobal = Value.MaxActionTokensGlobal; MaxNameUtf8Bytes = Value.MaxNameUtf8Bytes;
                MaxTextUtf8Bytes = Value.MaxTextUtf8Bytes; MaxTextUtf8BytesPerScreen = Value.MaxTextUtf8BytesPerScreen;
                MaxTrackedDirtyObjectsPerDomain = Value.MaxTrackedDirtyObjectsPerDomain; MaxCloneObjects = Value.MaxCloneObjects;
                MaxCloneDepth = Value.MaxCloneDepth; MaxProjectedElementsPerScreen = ProjectionLimit;
                MaxRenderElementsPerOperation = Value.MaxRenderElementsPerOperation;
                MaxRenderPropertiesPerElement = Value.MaxRenderPropertiesPerElement;
                MaxSerializedOperationBytes = Value.MaxSerializedOperationBytes;
                MaxPresentationSendsPerFlush = Value.MaxPresentationSendsPerFlush;
                MaxSerializedBytesPerFlush = Value.MaxSerializedBytesPerFlush; GuiFlushBudgetMicroseconds = Value.GuiFlushBudgetMicroseconds;
                PatchBatchesBeforeFull = Value.PatchBatchesBeforeFull;
                MaxPlayerInteractionsPerSecond = Value.MaxPlayerInteractionsPerSecond; MaxPlayerInteractionBurst = Value.MaxPlayerInteractionBurst;
                MaxActionInteractionsPerSecond = Value.MaxActionInteractionsPerSecond; MaxActionInteractionBurst = Value.MaxActionInteractionBurst;
            }
        }
    }
}
