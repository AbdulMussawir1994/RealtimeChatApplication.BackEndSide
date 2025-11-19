using App.Core.DTOs;
using App.Core.Entities;
using App.Core.Interface;
using App.Logic.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace App.Logic.HubContext
{
    [Authorize]
    public class WorkerHub : Hub<IWorkerHub>
    {

        private readonly IUnitOfWork unitOfWork;

        private readonly ICurrentUserService currentUserService;

        private readonly ConversationService conversationService;

        private readonly ILogger<WorkerHub> logger;

        private static readonly TimeSpan InactiveThreshold = TimeSpan.FromMinutes(5); // Customize as needed

        public WorkerHub(IUnitOfWork unitOfWork, ConversationService conversationService, ICurrentUserService currentUserService, ILogger<WorkerHub> logger)
        {
            this.unitOfWork = unitOfWork;
            this.conversationService = conversationService;
            this.currentUserService = currentUserService;
            this.logger = logger;
        }

        public async Task SendMessageFromWorker(ChatMessageDto dto)
        {
            await Clients.User(dto.SenderNumber).ReceiveMessageAsync(dto);

            await Clients.User(dto.ReceiverNumber).ReceiveMessageAsync(dto);

            await Clients.User(dto.SenderNumber)
                .UpdateNotifyClientMessageList(dto.SenderNumber, dto.ReceiverNumber);

            await Clients.User(dto.ReceiverNumber)
                .UpdateNotifyClientMessageList(dto.SenderNumber, dto.ReceiverNumber);

            // Check if receiver is inactive and send notification if needed
            await NotifyIfUserInactive(dto.ReceiverNumber, dto);
        }

        public async Task UpdateMessageList(string senderNumber, string receiverNumber)
        {
            await Clients.User(receiverNumber)
                .UpdateNotifyClientMessageList(senderNumber, receiverNumber);
        }

        public async Task UserTyping(string receiverNumber)
        {
            string? senderNumber = Context.User?.Claims.FirstOrDefault(x => x.Type == ClaimTypes.MobilePhone)?.Value;

            if (!string.IsNullOrEmpty(senderNumber))
            {
                await Clients.User(receiverNumber).UserTyping(senderNumber);
            }
        }

        public override async Task OnConnectedAsync()
        {

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await base.OnDisconnectedAsync(exception);
        }

        private async Task NotifyIfUserInactive(string userNumber, ChatMessageDto message)
        {
            // Get user info (e.g., Admin)
            var result = await conversationService.GetAllConversationsAsync();
            if (!result.IsSuccess || result.Data is null)
                return;

            // Find the user by number
            var user = result.Data
                .SelectMany(mb => new[] { mb.SenderNumber, mb.ReceiverNumber })
                .Distinct()
                .FirstOrDefault(n => n == userNumber);

            if (user is null)
                return;

            // You may want to use a UserManager or repository to get AppUser by number
            // For demonstration, let's assume you have a method to get AppUser by number
            var appUser = await GetAppUserByNumberAsync(userNumber);
            if (appUser == null)
                return;

            // Check inactivity
            if (!appUser.LastSeen.HasValue || DateTime.UtcNow - appUser.LastSeen.Value > InactiveThreshold)
            {
                // Send notification (customize as needed)
                await SendInactiveUserNotification(userNumber, message);
            }
        }

        // Example: Get AppUser by phone number (implement as needed)
        private async Task<AppUser?> GetAppUserByNumberAsync(string userNumber)
        {
            // Replace with your actual user lookup logic
            // For example, via UserManager or repository
            // Example:
            // return await userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == userNumber);
            return null;
        }

        // Example: Send notification to inactive user (customize as needed)
        private async Task SendInactiveUserNotification(string userNumber, ChatMessageDto message)
        {
            await Clients.User(userNumber)
                .ReceiveInactiveNotification(
                    $"You have a new message from {message.SenderNumber} while you were inactive.",
                    message
                );
        }
    }
}