using MassTransit.Configuration;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace SportsData.Api.Infrastructure.Notifications;

public interface INotificationService
{
    Task<Response?> SendEmailAsync(string toEmail, string templateId, object templateData);
    Task<MessageResource> SendSmsAsync(string toPhoneNumber, string message);
}


public class NotificationService : INotificationService
{
    private readonly string _sendGridApiKey;
    private readonly string _fromEmail;
    private readonly string _fromName;

    private readonly string _twilioAccountSid = string.Empty;
    private readonly string _twilioAuthToken = string.Empty;
    private readonly string _twilioPhoneNumber = string.Empty;

    public NotificationService(IOptions<NotificationConfig> config)
    {
        _sendGridApiKey = config.Value.Email.ApiKey;
        _fromEmail = config.Value.Email.FromEmail;
        _fromName = "sportDeets";

// Remove hardcoded defaults on the fields
-    private readonly string _twilioAccountSid = string.Empty;
-    private readonly string _twilioAuthToken = string.Empty;
    private readonly string _twilioAccountSid;
    private readonly string _twilioAuthToken;
    private readonly string _twilioPhoneNumber;

 public NotificationService(IOptions<NotificationConfig> config)
 {
     _sendGridApiKey = config.Value.Email.ApiKey;
     _fromEmail = config.Value.Email.FromEmail;
     _fromName = "sportDeets";

-        _twilioAccountSid = "ACc5c3d4c5ffbc90aaf6f0c22eaa8d51b2";
-        _twilioAuthToken = "79dedee4b076b6a64607044fa122c866";
        _twilioAccountSid = config.Value.Sms.AccountSid;
        _twilioAuthToken = config.Value.Sms.AuthToken;
        _twilioPhoneNumber = config.Value.Sms.PhoneNumber;

     TwilioClient.Init(_twilioAccountSid, _twilioAuthToken);
 }

        TwilioClient.Init(_twilioAccountSid, _twilioAuthToken);
    }

    public async Task<Response?> SendEmailAsync(string toEmail, string templateId, object templateData)
    {
        var client = new SendGridClient(_sendGridApiKey);
        var from = new EmailAddress(_fromEmail, _fromName);
        var to = new EmailAddress(toEmail);
        var msg = MailHelper.CreateSingleTemplateEmail(from, to, templateId, templateData);
        var response = await client.SendEmailAsync(msg);
        return response;
    }

    public async Task<MessageResource> SendSmsAsync(string toPhoneNumber, string message)
    {
        return await MessageResource.CreateAsync(
            to: new PhoneNumber(toPhoneNumber),
            from: new PhoneNumber(_twilioPhoneNumber),
            body: message
        );
    }
}