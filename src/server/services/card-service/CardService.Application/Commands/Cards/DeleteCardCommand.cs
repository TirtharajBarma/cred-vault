using CardService.Application.DTOs.Responses;
using MediatR;

namespace CardService.Application.Commands.Cards;

public sealed record DeleteCardCommand(Guid UserId, Guid CardId) : IRequest<OperationResult>;
