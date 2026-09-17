namespace Erp.FiscalPT.AtWebservice.Crypto;

/// <summary>The already-built, ready-to-serialize fields of the WS-Security UsernameToken.</summary>
public sealed record WsSecurityHeader(string Username, string Password, string Nonce, string Created);
