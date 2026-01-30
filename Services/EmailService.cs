using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

namespace backEndGamesTito.API.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendRecoveryEmailAsync(string emailDestino, string token)
        {
            var emailSettings = _configuration.GetSection("EmailSettings");

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(emailSettings["SenderName"], emailSettings["SenderEmail"]));
            message.To.Add(new MailboxAddress("", emailDestino));
            message.Subject = "Recuperação de Senha - GamesTito";

            // Corpo do E-mail em HTML
            var builder = new BodyBuilder();
            // Link fictício apontando para seu futuro frontend
            string link = $"http://localhost:3000/reset-password?token={token}";

            builder.HtmlBody = $@"
                <div style='font-family: Arial, sans-serif; padding: 20px;'>
                    <h2>Recuperação de Senha</h2>
                    <p>Você solicitou a troca de senha. Use o token abaixo ou clique no link:</p>
                    <h3 style='background-color: #f4f4f4; padding: 10px; display: inline-block;'>{token}</h3>
                    <p><a href='{link}'>Clique aqui para redefinir</a></p>
                    <p>Se não foi você, ignore este e-mail.</p>
                </div>";

            message.Body = builder.ToMessageBody();

            using (var client = new SmtpClient())
            {
                // Conexão segura com TLS
                await client.ConnectAsync(emailSettings["SmtpServer"], int.Parse(emailSettings["Port"]), MailKit.Security.SecureSocketOptions.StartTls);

                // Autenticação
                await client.AuthenticateAsync(emailSettings["SenderEmail"], emailSettings["Password"]);

                // Envio
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
            }
        }
    }
}
