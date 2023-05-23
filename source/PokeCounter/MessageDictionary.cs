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
        ReserveQueryHandle,
        DisposeQueryHandle,
        Increment,
        Decrement,
        SetValue,
        Save,
        /// <summary>
        /// wParam = EdgeHighlight enum
        /// </summary>
        SetEdgeHighlight,
    }
    /// <summary>
    /// Sends a struct immediately, is read by the reciever, and is disposed
    /// </summary>
    public enum Post
    {
        /// <summary>
        /// Expects struct LayoutData
        /// </summary>
        LayoutData = 0x400 + 100,
    }

    /// <summary>
    /// Asks for a specific kind of struct, posts it, and requires a dispose message before another query can be sent
    /// </summary>
    public enum Query
    {
        LayoutData = 0x400 + 200,
    }
}
