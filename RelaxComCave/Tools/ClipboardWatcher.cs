using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RelaxComCave.Tools {
    public class ClipboardWatcher {
        public static string GetClipboardData() {
            try {
                string clipboardData = null;
                Exception threadEx = null;
                Thread staThread = new Thread(
                    delegate () {
                        try {
                            clipboardData = Clipboard.GetText(TextDataFormat.Text);
                        } catch (Exception ex) {
                            threadEx = ex;
                        }
                    });
                staThread.SetApartmentState(ApartmentState.STA);
                staThread.Start();
                staThread.Join();
                return clipboardData;
            } catch (Exception exception) {
                return string.Empty;
            }
        }
    }
}
