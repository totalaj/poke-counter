using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PokeCounter
{
    public enum Message
    {
        Ping = 0x400,
        /// <summary>
        /// wParam = handle of window that send the handshake
        /// </summary>
        Handshake,
        /// <summary>
        /// wParam = handle of disconnecting window
        /// </summary>
        Disconnect,
        RefreshOtherWindows,
        ReserveQueryHandle,
        DisposeQueryHandle,
        Show,
        Hide,
        Increment,
        Decrement,
        SetValue,
        /// <summary>
        /// ReturnValue = value of counter
        /// </summary>
        GetValue,
        Save,
        /// <summary>
        /// wParam = EdgeHighlight enum
        /// </summary>
        SetEdgeHighlight,
        WriteProfilePathToGroupFile,
        /// <summary>
        /// wParam = Group index. -1 clears it from groups
        /// </summary>
        SetGroup,
        /// <summary>
        /// ReturnValue = Group index
        /// </summary>
        GetGroup,
        /// <summary>
        /// ReturnValue = 1 if groupShutdown is true, 0 if false
        /// </summary>
        GetGroupShutdown,
        EnterGroupElegibilityMode,
        ExitGroupElegibilityMode,
        /// <summary>
        /// wParam = eligibility bool. 0 is false, 1 is true
        /// </summary>
        SetEligibleForGroup,
        /// <summary>
        /// ReturnValue = eligibility bool. 0 is false, 1 is true
        /// </summary>
        GetEligibleForGroup,
        /// <summary>
        /// wParam = 1 is true, 0 is false
        /// </summary>
        SetGroupShutdown,
        RefreshMetaSettings,
        /// <summary>
        /// wParam = 0 is always close normally. 1 is close forcefully
        /// </summary>
        Close,

    }
    /// <summary>
    /// Sends a struct immediately, is read by the reciever, and is disposed
    /// </summary>
    public enum Post
    {
        /// <summary>
        /// Expects struct LayoutData
        /// </summary>
        LayoutData = 0x400 + 1000,
        /// <summary>
        /// Expects a Vector struct
        /// wParam = Optional group identifier. If this is -1, movement will be made no matter group index or group mode
        /// </summary>
        DeltaMovement,
    }

    /// <summary>
    /// Asks for a specific kind of struct, posts it, and requires a dispose message before another query can be sent
    /// </summary>
    public enum Query
    {
        LayoutData = 0x400 + 2000,
        /// <summary>
        /// GlobalHotkey struct
        /// </summary>
        GetIncrementKey,
        /// <summary>
        /// GlobalHotkey struct
        /// </summary>
        GetDecrementKey,
    }
}
