using App.Core.DTOs;
using App.Core.Interface;
using App.Logic.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
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

        private static readonly TimeSpan InactiveThreshold = TimeSpan.FromMinutes(5);

        // High-performance in-memory user tracking
        private static readonly ConcurrentDictionary<string, UserConnectionInfoDto> UserConnections =
            new ConcurrentDictionary<string, UserConnectionInfoDto>();

        public WorkerHub(IUnitOfWork unitOfWork, ConversationService conversationService, ICurrentUserService currentUserService, ILogger<WorkerHub> logger)
        {
            this.unitOfWork = unitOfWork;
            this.conversationService = conversationService;
            this.currentUserService = currentUserService;
            this.logger = logger;
        }

        public async Task SendMessageFromWorker(ChatMessageDto dto)
        {

            // 1. Check if receiver is inactive
            if (IsUserInactive(dto.ReceiverNumber))
            {
                await Clients.User(dto.SenderNumber)
                    .ReceiveInactiveNotification(
                        $"User {dto.ReceiverNumber} is inactive.",
                        dto
                    );

                return;
            }

            await Clients.User(dto.SenderNumber).ReceiveMessageAsync(dto);

            await Clients.User(dto.ReceiverNumber).ReceiveMessageAsync(dto);

            await Clients.User(dto.SenderNumber)
                .UpdateNotifyClientMessageList(dto.SenderNumber, dto.ReceiverNumber);

            await Clients.User(dto.ReceiverNumber)
                .UpdateNotifyClientMessageList(dto.SenderNumber, dto.ReceiverNumber);

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

        // ─────────────────────────────────────────────
        // USER CONNECTED
        // ─────────────────────────────────────────────
        public override async Task OnConnectedAsync()
        {
            var phone = Context.User?.FindFirst(ClaimTypes.MobilePhone)?.Value;

            if (!string.IsNullOrEmpty(phone))
            {
                UserConnections[phone] = new UserConnectionInfoDto
                {
                    ConnectionId = Context.ConnectionId,
                    LastSeen = DateTime.UtcNow,
                    IsActive = true
                };

                logger.LogInformation($"User Connected: {phone}");
            }

            await base.OnConnectedAsync();
        }

        // ─────────────────────────────────────────────
        // USER DISCONNECTED
        // ─────────────────────────────────────────────
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var phone = Context.User?.FindFirst(ClaimTypes.MobilePhone)?.Value;

            if (!string.IsNullOrEmpty(phone))
            {
                if (UserConnections.TryGetValue(phone, out var info))
                {
                    info.IsActive = false;
                    info.LastSeen = DateTime.UtcNow;
                }

                logger.LogInformation($"User Disconnected: {phone}");
            }

            await base.OnDisconnectedAsync(exception);
        }

        // ─────────────────────────────────────────────
        // ADMIN DEACTIVATES A USER (Real-Time Notify)
        // ─────────────────────────────────────────────
        public async Task NotifyInactiveByAdmin(string userNumber)
        {
            // Update DB (no UnitOfWork)
            await userService.DeactivateUserAsync(userNumber);

            // Notify if connected
            if (UserConnections.TryGetValue(userNumber, out var info))
            {
                await Clients.Client(info.ConnectionId)
                    .ReceiveInactiveNotification("Your account has been deactivated by the admin.", null);
            }
        }

        // ─────────────────────────────────────────────
        // CHECK USER INACTIVE
        // High-Performance: Pure Memory (No DB)
        // ─────────────────────────────────────────────
        private bool IsUserInactive(string phone)
        {
            if (!UserConnections.TryGetValue(phone, out var info))
                return true; // not connected → inactive

            if (!info.IsActive)
                return true;

            if (DateTime.UtcNow - info.LastSeen > InactiveThreshold)
                return true;

            return false;
        }
    }
}