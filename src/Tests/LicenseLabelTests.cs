using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

public static partial class LicenseLabel {
    public static List<string> Test() {
        var r = new List<string>();
        Action<bool, string> Check = (ok, what) => r.Add((ok ? "OK    " : "ERROR ") + what);
        string folder = Path.Combine(Path.GetTempPath(), "WM-test-license-" + Guid.NewGuid().ToString("N")), previousLanguage = Language.Code;
        string veryLong = new string('W', License.MAX_HOLDER);
        try {
            Directory.CreateDirectory(folder);
            Settings.UseFile(Path.Combine(folder, "settings.ini"));
            using (var pair = CngKey.Create(CngAlgorithm.ECDsaP256, null, new CngKeyCreationParameters()))
            using (var other = CngKey.Create(CngAlgorithm.ECDsaP256, null, new CngKeyCreationParameters())) {
                var my = new[] { License.KeyAsText(pair.Export(CngKeyBlobFormat.EccPublicBlob)) };
                Func<CngKey, string, int, string, string> Signature = (k, program, num, name) => {
                    string data = License.Compose(program, num, name, "2026-11-03");
                    using (var e = new ECDsaCng(k)) { e.HashAlgorithm = CngAlgorithm.Sha256; return License.Join(data, e.SignData(License.SignedBytes(data))); }
                };
                var none = new int[0];
                string good = Signature(pair, PROGRAM, 7, "Smith & Sons Ltd");

                var v = License.Check(good, PROGRAM, my, none);
                Check(v.Valid && v.Holder == "Smith & Sons Ltd" && v.Number == 7 && v.Data == "2026-11-03" && v.Program == PROGRAM, "valid: read from the signed data");
                Check(License.Check("  " + good.Insert(20, "\r\n") + " \n", PROGRAM, my, none).Valid, "pasted with spaces and line breaks: still valid");
                var a = License.Check(Signature(pair, "ProjectManager", 8, "Smith"), PROGRAM, my, none);
                Check(a.State == LicenseState.OtherProgram && !a.Valid && a.Program == "ProjectManager", "of another program: not valid");
                Check(License.Check(good, PROGRAM, my, new[] { 3, 7 }).State == LicenseState.Revoked, "revoked: not valid");
                var pieces = good.Split('.');
                string brokenSignature = pieces[2].Substring(0, 5) + (pieces[2][5] == 'A' ? 'B' : 'A') + pieces[2].Substring(6);
                var height = License.Check(pieces[0] + "." + pieces[1] + "." + brokenSignature, PROGRAM, my, none);
                Check(height.State == LicenseState.Invalid && height.Holder == "", "altered signature: not valid and shows no name");
                var dat = License.Check(pieces[0] + "." + License.Compose(PROGRAM, 7, "Somebody Else", "2026-11-03") + "." + pieces[2], PROGRAM, my, none);
                Check(dat.State == LicenseState.Invalid && dat.Holder == "", "data changed with the earlier signature: not valid");
                Check(License.Check(Signature(other, PROGRAM, 7, "Smith"), PROGRAM, my, none).State == LicenseState.Invalid, "signed with another key: not valid");
                Check(License.Check(good, PROGRAM).State == LicenseState.Invalid, "signed with the test key: not valid for the real program");
                foreach (var t in new[] { "hello", "", null, "NSCENT-1.x.y", "NSCENT-2." + pieces[1] + "." + pieces[2], pieces[1] + "." + pieces[2], good + ".x", new string('a', 20000) })
                    Check(License.Check(t, PROGRAM, my, none).State == LicenseState.Invalid, "wrong text: \"" + (t == null ? "null" : t.Length > 24 ? t.Substring(0, 24) + "..." : t) + "\"");
                var longText = License.Check(Signature(pair, PROGRAM, 9, veryLong), PROGRAM, my, none);
                Check(longText.Valid && longText.Holder.Length == 80, "holder of 80 characters: valid and whole");
                Check(License.Compose(PROGRAM, 9, veryLong + "W", "2026-11-03") == null, "holder of 81 characters: cannot be composed");

                keys = my; revoked = none; now = null;
                string file = Path.Combine(folder, License.FILE);
                bool ok;
                Check(Current.State == LicenseState.Absent && !Current.Valid && !HasFile(), "absent: it is personal");
                Language.Use("it");
                Check(Caption() == Language.T("license.personal") && FlyoutCaption() == Language.T("overview.version", "version", Program.VERSION) + " · " + Language.T("overview.license_personal"), "absent: the two captions");
                string m = Use("wrong", out ok);
                Check(!ok && !File.Exists(file) && !Current.Valid && m.Contains(Language.T("license.msg.invalid")) && m.Contains(Language.T("license.msg.keeps_personal")), "wrong text with the button: it says so, writes nothing, stays personal");
                m = Use("  \r\n ", out ok);
                Check(!ok && !File.Exists(file) && m == Language.T("license.msg.empty"), "empty box: asks to paste");
                m = Use(Signature(pair, "ProjectManager", 8, "Smith"), out ok);
                Check(!ok && !File.Exists(file) && m.Contains("ProjectManager"), "of another program with the button: it says so and writes nothing");
                m = Use(good, out ok);
                Check(ok && File.Exists(file) && Current.Valid && Caption() == Language.T("license.commercial_of", "name", "Smith & Sons Ltd"), "\"Use this license\": written and in use at once");
                Check(FlyoutCaption() == Language.T("overview.version", "version", Program.VERSION) + " · " + Language.T("overview.license_commercial", "name", "Smith & Sons Ltd"), "in the flyout: the commercial license with the name");
                now = null;
                Check(Current.Valid && Current.Number == 7, "on reopening it is read again from the file");
                m = Use("wrong", out ok);
                Check(!ok && Current.Valid && License.Load(folder, PROGRAM, my, none).Valid && m == Language.T("license.msg.invalid") + " " + Language.T("license.msg.keeps_commercial"), "wrong text with an already good license: the good one stays");
                m = Remove(out ok);
                Check(ok && !File.Exists(file) && !Current.Valid && !HasFile() && m.Contains(Language.T("license.msg.keeps_personal")), "\"Remove the license\": the file goes away, back to personal");
                Remove(out ok);
                Check(ok, "removing when there is nothing: fine anyway");

                using (var f = new LicenseWindow(1f)) {
                    Check(f.StateTest == Language.T("license.personal") && !f.YesRemovesTest, "window: starts personal, nothing to remove");
                    f.UseTest("wrong");
                    Check(f.StateTest == Language.T("license.personal") && f.SentenceTest.Contains(Language.T("license.msg.invalid")) && f.BoxTest == "wrong" && !f.YesRemovesTest, "window: wrong text, it says so and the text stays to be corrected");
                    f.UseTest(good);
                    Check(f.StateTest == Language.T("license.commercial_of", "name", "Smith & Sons Ltd") && f.SentenceTest == Language.T("license.msg.saved") && f.BoxTest == "" && f.YesRemovesTest && File.Exists(file), "window: \"Use this license\" shows the name and the box empties");
                    f.RemoveTest();
                    Check(f.StateTest == Language.T("license.personal") && !f.YesRemovesTest && !File.Exists(file) && f.SentenceTest.Contains(Language.T("license.msg.keeps_personal")), "window: \"Remove the license\" back to personal");
                }
                using (var f = new SettingsWindow(1f)) Check(f.LicenseClickTest(), "Settings: the click on the caption opens the license window");

                License.Write(folder, good);
                Check(License.Load(folder, PROGRAM, my, new[] { 7 }).State == LicenseState.Revoked, "file of a license later revoked: revoked on reopening");
                File.WriteAllText(file, "broken\r\n", new UTF8Encoding(false));
                now = null;
                Check(Current.State == LicenseState.Invalid && !Current.Valid && HasFile() && Problem(Current) != "", "broken file: it says so, it is personal and can be removed");
                File.WriteAllBytes(file, new byte[10000]);
                now = null;
                Check(Current.State == LicenseState.Invalid && Caption() == Language.T("license.personal"), "huge file: it is personal");
                File.Delete(file);
                Directory.CreateDirectory(file);
                now = null;
                Check(Current.State == LicenseState.Unreadable && !Current.Valid && Problem(Current) != "", "unreadable file: it says so and it is personal");
                Check(!License.Write(folder, good), "folder that cannot be written: Write says no");
                m = Use(good, out ok);
                Check(!ok && !Current.Valid && m.StartsWith(Language.T("license.msg.not_saved"), StringComparison.Ordinal), "cannot be saved: it says so and stays personal");
                m = Remove(out ok);
                Check(!ok && m.StartsWith(Language.T("license.msg.not_removed"), StringComparison.Ordinal), "cannot be removed: it says so");
                Directory.Delete(file);
                now = null;
            }

            foreach (var c in Language.CODES) {
                Language.Use(c);
                string sentence = Language.T("license.sale");
                string site = c == "it" ? "www.nscent.art/software" : "www.nscent.art/" + c + "/software";
                int where = sentence.IndexOf(site, StringComparison.Ordinal);
                Check(where >= 0 && !char.IsLetter(sentence[where + site.Length]) && sentence.Contains("info@nscent.art") && !sentence.Contains("http") && !sentence.Contains("gmail"), c + ": the sentence leads to info@nscent.art and to " + site + ", as text");
                foreach (var st in new[] { LicenseState.Invalid, LicenseState.OtherProgram, LicenseState.Revoked, LicenseState.Unreadable })
                    Check(Problem(new LicenseResult { State = st, Program = "ProjectManager" }).Length > 0, c + ": sentence for " + st);
                Check(!Language.T("license.commercial_of", "name", "x").Contains("{"), c + ": no placeholder left");
                foreach (var name in new[] { null, "Smith & Sons Ltd", veryLong, "iiii iiii iiii iiii iiii iiii iiii iiii iiii iiii iiii iiii iiii iiii iiii iiii" }) {
                    SampleData(name);
                    string of = c + (name == null ? ", personal: " : ", " + name.Length + " characters: ");
                    r.AddRange(Drawing.LicenseCaptionTest(of, name == null));
                    using (var f = new SettingsWindow(1f)) r.AddRange(f.LicenseCaptionTest(of, name == null));
                    foreach (var st in new[] { LicenseState.Absent, LicenseState.OtherProgram, LicenseState.Unreadable })
                        using (var f = new LicenseWindow(1f)) {
                            if (st != LicenseState.Absent) f.Sentence(Problem(new LicenseResult { State = st, Program = "ProjectManager" }) + " " + Stays(), true);
                            r.AddRange(f.SpacesTest(of + st + ": "));
                        }
                }
            }
        } finally {
            keys = License.PUBLIC_KEYS; revoked = License.REVOKED; now = null;
            Language.Use(previousLanguage);
            try { Directory.Delete(folder, true); } catch { }
        }
        return r;
    }
}
