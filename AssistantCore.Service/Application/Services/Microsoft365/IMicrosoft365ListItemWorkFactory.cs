using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Repositories;
using AssistantCore.Service.Application.Models.Microsoft365;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365ListItemWorkFactory
{
    Microsoft365ListItemWorkData Create(
        Microsoft365List list,
        Microsoft365ListItemDelta item,
        DateTimeOffset createdAt);
}
