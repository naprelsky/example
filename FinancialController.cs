using System;
using System.Linq;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;


namespace CoreAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FinancialController : HMBaseController
    {
        public FinancialController(ApplicationContext context, IOptions<HMConfiguration> config) : base(context,config)
        {
        }

        [HttpGet("{clientID}/balance")]
        public IActionResult GetBalance(int clientID)
        {
            if (!IsAuthorized())
            {
                return Unauthorized();
            }

            var result = db.Balances.OrderByDescending(l => l.CreationDate).Where(l => l.ClientID == clientID).Select(l => l.Amount).First();
            return Ok(result);
        }

        [HttpPost("{clientID}/deposit")]
        public IActionResult Deposit(int clientID)
        {
            if (!IsAuthorized())
            {
                return Unauthorized();
            }

            var headers = Request.Headers;

            if (!headers.TryGetValue("document_guid", out var documentGUID))
            {
                Response.StatusCode = (int)System.Net.HttpStatusCode.BadRequest;
                return new JsonResult("В запросе остуствует обязательный заголовок 'document_guid'");
            }

            if (!headers.TryGetValue("amount", out var amount))
            {
                Response.StatusCode = (int)System.Net.HttpStatusCode.BadRequest;
                return new JsonResult("В запросе остуствует обязательный заголовок 'amount'");
            }

            headers.TryGetValue("payment_number", out var payment_number);

            var payment = db.Payments.Where(l => l.ClientID == clientID && l.DocumentGUID == documentGUID).FirstOrDefault();
            if (payment != null)
            {
                Response.StatusCode = (int)System.Net.HttpStatusCode.Conflict;
                return new JsonResult("В базе данных уже существует документ с GUID: " + documentGUID);
            }

            payment = new Payment();
            payment.ClientID = Convert.ToInt32(clientID);
            payment.DocumentGUID = documentGUID;
            payment.TransactionTypeID = 2;
            payment.Amount = Convert.ToDecimal(amount);
            payment.PaymentNumber = payment_number;
            payment.CreationDate = DateTime.UtcNow;

            db.Payments.Add(payment);
            try
            {
                db.SaveChanges();
            }
            catch (DbUpdateException)
            {
                Response.StatusCode = (int)System.Net.HttpStatusCode.InternalServerError;
                return new JsonResult("Ошибка при сохранении платежного документа в базу данных. GUID: " + documentGUID);
            }

            return Ok(payment);
        }

        [HttpDelete("{clientID}/deposit")]
        public IActionResult DepositDelete(int client_id)
        {
            if (!IsAuthorized())
            {
                return Unauthorized();
            }

            if (!Request.Headers.TryGetValue("document_guid", out var documentGUID))
            {
                Response.StatusCode = (int)System.Net.HttpStatusCode.BadRequest;
                return new JsonResult("В запросе остуствует обязательный заголовок 'document_guid'");
            }

            var payment = db.Payments.Where(l => l.ClientID == client_id && l.DocumentGUID == documentGUID).FirstOrDefault();
            if (payment == null)
            {
                Response.StatusCode = (int)System.Net.HttpStatusCode.NotFound;
                return new JsonResult("В базе данных отсутствует документ с GUID: " + documentGUID);
            }

            db.Payments.Remove(payment);
            try
            {
                db.SaveChanges();
            }
            catch (DbUpdateException)
            {
                Response.StatusCode = (int)System.Net.HttpStatusCode.InternalServerError;
                return new JsonResult("Не удалось удалить документ с GUID: " + documentGUID);
            }

            return NoContent();
        }
    }
}
