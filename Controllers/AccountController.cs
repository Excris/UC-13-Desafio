// --- Controllers/AccountController.cs
using backEndGamesTito.Api.Models;
using backEndGamesTito.API.Models;
using backEndGamesTito.API.Repositories;
// 1. IMPORT DO SERVICE
using backEndGamesTito.API.Services;
using BCrypt.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

// Usar o banco de dados com o DbUsuario e os atributos da classe Usuario
using DbUsuario = backEndGamesTito.API.Data.Models.Usuario;

namespace backEndGamesTito.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountController : ControllerBase
    {
        private readonly UsuarioRepository _usuarioRepository;
        // 2. DECLARAÇÃO DO EMAIL SERVICE
        private readonly EmailService _emailService;

        // 3. CONSTRUTOR ATUALIZADO (INJEÇÃO DE DEPENDÊNCIA)
        public AccountController(UsuarioRepository usuarioRepository, EmailService emailService)
        {
            _usuarioRepository = usuarioRepository;
            _emailService = emailService;
        }

        // ===================================================================================
        // ROTAS DE AUTENTICAÇÃO
        // ===================================================================================

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequestModel model)
        {
            try
            {
                DateTime agora = DateTime.Now;
                string dataString = agora.ToString();
                string ApiKey = "mangaPara_todos_ComLeite_kkk";

                // --- CRIPTOGRAFIA PADRÃO (LÓGICA COMPLEXA) ---
                string PassSHA256 = ComputeSha256Hash(model.PassWordHash);
                string EmailSHA256 = ComputeSha256Hash(model.Email);

                // Montagem da String
                string PassCrip = PassSHA256 + EmailSHA256 + ApiKey;
                string HashCrip = EmailSHA256 + PassSHA256 + dataString + ApiKey; // Token inicial (opcional)

                // Hashing Final com BCrypt
                string PassBCrypt = BCrypt.Net.BCrypt.HashPassword(PassCrip);
                string HashBCrypt = BCrypt.Net.BCrypt.HashPassword(HashCrip);

                var novoUsuario = new DbUsuario
                {
                    NomeCompleto = model.NomeCompleto,
                    Email = model.Email,
                    PassWordHash = PassBCrypt,
                    HashPass = HashBCrypt,
                    DataAtualizacao = DateTime.Now,
                    StatusId = 2
                };

                await _usuarioRepository.CreateUserAsync(novoUsuario);

                return Ok(new
                {
                    erro = false,
                    message = "Usuário cadastrado com sucesso!",
                    usuario = new { model.NomeCompleto, model.Email }
                });
            }
            catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
            {
                return Conflict(new { erro = true, message = "Este email já está em uso!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { erro = true, message = "Sistema indisponivel no momento.", codErro = ex.Message });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestModel model)
        {
            try
            {
                var usuario = await _usuarioRepository.GetUserByEmailAsync(model.Email);

                if (usuario == null)
                {
                    return Unauthorized(new { erro = true, message = "E-mail ou senha inválidos." });
                }

                // --- REPETIÇÃO DA LÓGICA DE CRIPTOGRAFIA (OBRIGATÓRIO SER IGUAL AO REGISTER) ---
                string ApiKey = "mangaPara_todos_ComLeite_kkk";

                string PassSHA256 = ComputeSha256Hash(model.PassWordHash);
                string EmailSHA256 = ComputeSha256Hash(model.Email);

                string PassCripParaVerificar = PassSHA256 + EmailSHA256 + ApiKey;

                // Comparação BCrypt
                if (!BCrypt.Net.BCrypt.Verify(PassCripParaVerificar, usuario.PassWordHash))
                {
                    return Unauthorized(new { erro = true, message = "E-mail ou senha inválidos." });
                }

                return Ok(new
                {
                    erro = false,
                    message = "Login realizado com sucesso!",
                    usuario = new { usuario.NomeCompleto, usuario.Email }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { erro = true, message = "Erro ao processar login.", codErro = ex.Message });
            }
        }

        // ===================================================================================
        // ROTAS DE RECUPERAÇÃO DE SENHA
        // ===================================================================================

        // 4. NOVO MÉTODO: SOLICITAR RECUPERAÇÃO (ENVIA O E-MAIL)
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordModel model)
        {
            try
            {
                var usuario = await _usuarioRepository.GetUserByEmailAsync(model.Email);

                // Segurança: Resposta padrão mesmo se o e-mail não existir
                if (usuario == null)
                    return Ok(new { erro = false, message = "Se o e-mail existir, as instruções foram enviadas." });

                // Gera Token Único
                string token = Guid.NewGuid().ToString("N");

                // Salva o Token no Banco (Reaproveitando campo HashPass temporariamente ou campo específico)
                // Nota: Certifique-se de que o método UpdatePasswordAsync ou similar trate isso, 
                // ou crie um UpdateRecoveryTokenAsync no repositório.
                // Para simplificar aqui, vamos assumir que o UpdatePasswordAsync pode ser usado ou adaptado,
                // mas idealmente teríamos: await _usuarioRepository.SaveRecoveryTokenAsync(usuario.UsuarioId, token);
                // Vou usar uma lógica genérica de update aqui para ilustrar o fluxo:

                // *ATENÇÃO*: Você precisa garantir que tem um método no Repository para salvar SÓ o token.
                // Vou assumir que você criou o 'UpdateRecoveryTokenAsync' conforme conversamos.
                // Se não criou, use o UpdatePasswordAsync passando a senha atual (gambiarra) ou crie o método.
                await _usuarioRepository.UpdateRecoveryTokenAsync(usuario.UsuarioId, token);

                // Envia o E-mail
                await _emailService.SendRecoveryEmailAsync(usuario.Email, token);

                return Ok(new { erro = false, message = "E-mail enviado com sucesso!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message); // Log interno
                return StatusCode(500, new { erro = true, message = "Erro ao enviar e-mail." });
            }
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordModel model)
        {
            try
            {
                // Passo A: Busca pelo token
                var usuario = await _usuarioRepository.GetUserByTokenAsync(model.Token);

                // Passo B: Validações
                if (usuario == null || string.IsNullOrEmpty(usuario.HashPass))
                {
                    return BadRequest(new { erro = true, message = "Token inválido ou já utilizado!" });
                }

                if (usuario.DataAtualizacao.HasValue && usuario.DataAtualizacao.Value.AddMinutes(15) < DateTime.Now)
                {
                    return BadRequest(new { erro = true, message = "Link expirado!" });
                }

                // Passo C: Atualização da Senha com a Lógica CORRETA
                // *** CORREÇÃO: Usar a mesma lógica do Register/Login ***
                string ApiKey = "mangaPara_todos_ComLeite_kkk";

                string PassSHA256 = ComputeSha256Hash(model.NewPassword);
                string EmailSHA256 = ComputeSha256Hash(usuario.Email); // Pegamos o e-mail do usuário recuperado do banco

                string PassCrip = PassSHA256 + EmailSHA256 + ApiKey;

                // Gera o Hash final compatível com o Login
                string newPassBCrypt = BCrypt.Net.BCrypt.HashPassword(PassCrip);

                await _usuarioRepository.UpdatePasswordAsync(usuario.UsuarioId, newPassBCrypt);

                return Ok(new { erro = false, message = "Senha redefinida com sucesso!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { erro = true, message = "Erro interno no servidor.", codErro = ex.Message });
            }
        }

        // ===================================================================================
        // MÉTODOS AUXILIARES
        // ===================================================================================
        private string ComputeSha256Hash(string rawData)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }
    }
}