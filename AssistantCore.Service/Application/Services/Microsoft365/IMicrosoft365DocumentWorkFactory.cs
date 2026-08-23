using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Repositories;
using AssistantCore.Service.Application.Models.Microsoft365;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365DocumentWorkFactory
{
    Microsoft365DocumentWorkData Create(
        Microsoft365Drive drive,
        Microsoft365DriveItemDelta item,
        DateTimeOffset createdAt);
}
