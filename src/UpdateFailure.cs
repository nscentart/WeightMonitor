using System;

public class UpdateFailure : Exception {
    public readonly string LogText;

    public UpdateFailure(string shown, string logText) : base(shown) { LogText = logText; }
}
