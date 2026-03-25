using CardService.Application.DTOs.Responses;
using MediatR;

namespace CardService.Application.Commands.Cards;

public sealed record UpdateCardCommand(
    Guid UserId,
    Guid CardId,
    string CardholderName,
    int ExpMonth,
    int ExpYear,
    decimal CreditLimit,
    decimal OutstandingBalance,
    int BillingCycleStartDay,
    bool IsDefault) : IRequest<CardResult>;
