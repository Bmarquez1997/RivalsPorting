using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using DesktopNotifications;
using DynamicData.Binding;
using FluentAvalonia.UI.Controls;
using FortnitePorting.Application;
using FortnitePorting.Export;
using FortnitePorting.Models.Chat;
using FortnitePorting.Multiplayer.Extensions;
using FortnitePorting.Multiplayer.Models;
using FortnitePorting.Multiplayer.Packet;
using FortnitePorting.Shared;
using FortnitePorting.Shared.Extensions;
using FortnitePorting.Shared.Framework;
using FortnitePorting.Shared.Services;
using FortnitePorting.ViewModels;
using FortnitePorting.Views;
using Microsoft.Toolkit.Uwp.Notifications;
using Serilog;
using Serilog.Core;
using WatsonTcp;
using Exception = System.Exception;
using MultiplayerGlobals = FortnitePorting.Multiplayer.MultiplayerGlobals;

namespace FortnitePorting.Services;

public static class GlobalChatService
{
    public static bool EstablishedFirstConnection;
    public static WatsonTcpClient? Client;

    public static MetadataBuilder DefaultMeta => new MetadataBuilder()
        .With("Token", AppSettings.Current.Discord.Auth.AccessToken);
    
    public static void Init()
    {
        ViewModelRegistry.Register<ChatViewModel>();
        Client = new WatsonTcpClient(MultiplayerGlobals.SOCKET_IP, MultiplayerGlobals.SOCKET_PORT);
        Client.Settings.Guid = AppSettings.Current.Discord.Id;
        Client.Callbacks.SyncRequestReceivedAsync = SyncRequestReceivedAsync;
        Client.Events.MessageReceived += OnMessageReceived;
        Client.Connect();

        TaskService.Run(() =>
        {
            while (true)
            {
                while (!Client.Connected)
                {
                    try
                    {
                        Client.Connect();
                    }
                    catch (Exception)
                    {
                        // lol
                    }
                }
            }
        });
    }

    public static void DeInit()
    {
        Client?.Disconnect();
        Client?.Dispose();
    }

    private static async Task<SyncResponse> SyncRequestReceivedAsync(SyncRequest arg)
    {
        var type = arg.GetArgument<EPacketType>("Type");
        switch (type)
        {
            case EPacketType.Connect:
            {
                var response = new SyncResponse(arg, DefaultMeta.With("RequestingMessageHistory", !EstablishedFirstConnection).Build(), Array.Empty<byte>());;
                EstablishedFirstConnection = true;
                return response;
            }
            case EPacketType.Ping:
            {
                return new SyncResponse(arg, DefaultMeta.Build(), Array.Empty<byte>());
            }
            default:
            {
                throw new NotImplementedException(type.ToString());
            }
        }
    }

    private static async void OnMessageReceived(object? sender, MessageReceivedEventArgs e)
    {
        var type = e.GetArgument<EPacketType>("Type");
        var user = e.GetArgument<Identification>("User");
        switch (type)
        {
            case EPacketType.Permissions:
            {
                var permissions = e.Data.ReadPacket<PermissionsPacket>();

                ChatVM.Permissions = permissions.Permissions; // lol?
                break;
            }
            case EPacketType.Message:
            {
                var messagePacket = e.Data.ReadPacket<MessagePacket>();
                
                Bitmap? bitmap = null;
                if (messagePacket.HasAttachmentData)
                {
                    var stream = new MemoryStream(messagePacket.AttachmentData);
                    bitmap = new Bitmap(stream);
                }

                var isInChatView = AppVM.IsInView<ChatView>();
                var isPrivate = e.GetArgument<bool>("IsPrivate");
                if (isPrivate && !isInChatView)
                {
                    var notification = new Notification
                    {
                        Title = $"Message from {user.DisplayName}",
                        Body = messagePacket.Message
                    };
                    
                    await NotificationManager.ShowNotification(notification);
                }

                if (!e.GetArgument<bool>("IsMessageHistory") && !isInChatView)
                {
                    AppVM.ChatNotifications++;
                }
                
                ChatVM.Messages.InsertSorted(new ChatMessage
                {
                    User = new ChatUser
                    {
                        DisplayName = user.DisplayName,
                        UserName = user.UserName,
                        ProfilePictureURL = user.AvatarURL,
                        Id = user.Id,
                        Role = user.RoleType
                    },
                    Text = messagePacket.Message,
                    Id = e.GetArgument<Guid>("ID"),
                    Timestamp = e.GetArgument<DateTime>("Timestamp").ToLocalTime(),
                    Bitmap = bitmap,
                    BitmapName = messagePacket.AttachmentName,
                    ReactionCount = e.GetArgument<int>("Reactions"),
                    ReactedTo = e.GetArgument<bool>("HasReacted"),
                    IsPrivate = isPrivate,
                    TargetUserName = e.GetArgument<string>("TargetUserName")
                }, SortExpressionComparer<ChatMessage>.Descending(message => message.Timestamp));
                break;
            }
            case EPacketType.OnlineUsers:
            {
                var onlineUsers = e.Data.ReadPacket<OnlineUsersPacket>().Users;
                
                for (var i = 0; i < ChatVM.Users.Count; i++)
                {
                    if (onlineUsers.Any(onlineUser => ChatVM.Users[i].UserName.Equals(onlineUser.UserName))) continue;
                    ChatVM.Users.RemoveAt(i);
                }
                
                for (var i = 0; i < onlineUsers.Count; i++)
                {
                    var onlineUser = onlineUsers[i];
                    if (ChatVM.Users.FirstOrDefault(existingUser => existingUser.UserName.Equals(onlineUser.UserName)) 
                        is { } realUser)
                    {
                        realUser.Role = onlineUser.RoleType; // update role if changed
                        continue;
                    }
                    
                    ChatVM.Users.Add(new ChatUser
                    {
                        DisplayName = onlineUser.DisplayName,
                        UserName = onlineUser.UserName,
                        ProfilePictureURL = onlineUser.AvatarURL,
                        Id = onlineUser.Id,
                        Role = onlineUser.RoleType
                    });
                }
                
                break;
            }
            case EPacketType.Reaction:
            {
                var reaction = e.Data.ReadPacket<ReactionPacket>();
                var targetMessage = ChatVM.Messages.FirstOrDefault(message => message.Id == reaction.MessageId);
                if (targetMessage is null) break;

                targetMessage.ReactionCount += reaction.Increment ? 1 : -1;
                break;
            }
            case EPacketType.Export:
            {
                var export = e.Data.ReadPacket<ExportPacket>();
                
                TaskService.RunDispatcher(async () =>
                {
                    var dialog = new ContentDialog
                    {
                        Title = "Incoming Export Request",
                        Content = $"{user.DisplayName} sent a request to export \"{export.Path}.\" Accept?",
                        CloseButtonText = "No",
                        PrimaryButtonText = "Yes",
                        PrimaryButtonCommand = new RelayCommand(async () =>
                        {
                            var asset = await CUE4ParseVM.Provider.TryLoadObjectAsync(Exporter.FixPath(export.Path));
                            if (asset is null) return;

                            var exportType = Exporter.DetermineExportType(asset);
                            if (exportType is EExportType.None)
                            {
                                DisplayDialog("Unimplemented Exporter",
                                    $"A file exporter for \"{asset.ExportType}\" assets has not been implemented and/or will not be supported.");
                            }
                            else
                            {
                                await Exporter.Export(asset, exportType, AppSettings.Current.CreateExportMeta());
                            }

                        })
                    };

                    await dialog.ShowAsync();
                });

                break;
            }
            default:
            {
                throw new NotImplementedException(type.ToString());
            }
        }
    }
    
    public static async Task Send(IPacket packet, MetadataBuilder? additionalMeta = null)
    {
        await Send(packet.WritePacket(), packet.PacketType, additionalMeta);
    }
    
    public static async Task Send(byte[] data, EPacketType type, MetadataBuilder? additionalMeta = null)
    {
        var meta = DefaultMeta
            .WithType(type)
            .With(additionalMeta);

        await Client.SendAsync(data, meta.Build());
    }
}