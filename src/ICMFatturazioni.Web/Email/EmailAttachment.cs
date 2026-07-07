namespace ICMFatturazioni.Web.Email;

/// <summary>
/// Allegato binario di un'email transazionale (es. il PDF di cortesia di un
/// avviso/fattura). <paramref name="ContentType"/> è il MIME type
/// (es. <c>application/pdf</c>). Mirror di ICMVerbali.
/// </summary>
public sealed record EmailAttachment(string FileName, byte[] Content, string ContentType);
