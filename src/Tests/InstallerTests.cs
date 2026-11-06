using System;
using System.Windows.Forms;

public static partial class Installer {
    public static string SignatureTest(string file) {
        bool valid = ValidSignature(file);
        string who; try { who = Signer(file); } catch (Exception ex) { who = "(" + ex.Message + ")"; }
        return "signature " + (valid ? "valid" : "NOT valid") + ", certificate of " + who;
    }
}
