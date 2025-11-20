using App.Core.DTOs;
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

        private readonly IConnectionManager _connection;

        public WorkerHub(IUnitOfWork unitOfWork, ConversationService conversationService,
                                            ICurrentUserService currentUserService, ILogger<WorkerHub> logger, IConnectionManager connection)
        {
            this.unitOfWork = unitOfWork;
            this.conversationService = conversationService;
            this.currentUserService = currentUserService;
            this.logger = logger;
            _connection = connection;
        }

        public async Task SendMessageFromWorker(ChatMessageDto dto)
        {

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

        public override Task OnConnectedAsync()
        {
            string? user = Context.User?.FindFirst(ClaimTypes.MobilePhone)?.Value;

            if (!string.IsNullOrEmpty(user))
            {
                _connection.AddConnection(user, Context.ConnectionId);
                logger.LogInformation($"User connected: {user}");
            }

            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            string? user = Context.User?.FindFirst(ClaimTypes.MobilePhone)?.Value;

            if (!string.IsNullOrEmpty(user))
            {
                _connection.RemoveConnection(user, Context.ConnectionId);
                logger.LogInformation($"User disconnected: {user}");
            }

            return base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        ///     Admin triggers this → user receives real-time logout
        /// </summary>
        public async Task ForceLogoutUser(string userNumber)
        {
            var connections = _connection.GetConnections(userNumber);

            if (connections.Count == 0)
                return;

            foreach (var connectionId in connections)
            {
                await Clients.Client(connectionId)
                    .ReceiveInactiveNotification("Your account has been deactivated by the admin.", null);
            }

        }
    }
}