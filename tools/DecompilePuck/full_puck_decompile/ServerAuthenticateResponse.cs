public class ServerAuthenticateResponse
{
	public bool success { get; set; }

	public string error { get; set; }

	public string ipAddress { get; set; }

	public bool isAuthenticated { get; set; }
}
