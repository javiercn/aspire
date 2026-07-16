// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Aspire.Dashboard.Components.CustomIcons;
using Aspire.Dashboard.Otlp.Model;
using Aspire.Dashboard.Resources;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Aspire.Dashboard.Model.GenAI;

[DebuggerDisplay("Index = {Index}, Type = {Type}, ResourceName = {ResourceName}")]
public class GenAIItemViewModel
{
    private static readonly Icon s_toolCallsIcon = new Octicons.Size16.Code();
    private static readonly Icon s_messageIcon = new Octicons.Size16.Mail();
    private static readonly Icon s_errorIcon = new Octicons.Size16.ErrorCircle();

    private static readonly Icon s_personIcon = new Octicons.Size16.Person();
    private static readonly Icon s_systemIcon = new Octicons.Size16.Laptop();
    private static readonly Icon s_toolIcon = new Octicons.Size20.CodeCircle(); // used in 16px size
    private static readonly Icon s_cloudErrorIcon = new Octicons.Size16.CloudError();

    public required int Index { get; set; }
    public required long? InternalId { get; init; }
    public required OtlpSpan Parent { get; init; }
    public required GenAIItemType Type { get; init; }
    public required List<GenAIItemPartViewModel> ItemParts { get; init; } = [];
    public required string ResourceName { get; init; }

    public BadgeDetail GetCategoryBadge(IStringLocalizer<Dialogs> loc)
    {
        if (Type == GenAIItemType.Error)
        {
            return new BadgeDetail(loc[nameof(Dialogs.GenAIMessageCategoryStatus)], "output", s_errorIcon);
        }
        if (Type == GenAIItemType.OutputMessage)
        {
            if (ItemParts.Any(p => p.MessagePart?.Type is MessagePart.ToolCallType or MessagePart.ServerToolCallType))
            {
                return new BadgeDetail(loc[nameof(Dialogs.GenAIMessageCategoryToolCalls)], "output", s_toolCallsIcon);
            }
            else
            {
                return new BadgeDetail(loc[nameof(Dialogs.GenAIMessageCategoryOutput)], "output", s_messageIcon);
            }
        }
        if (ItemParts.Any(p => p.MessagePart?.Type is MessagePart.ToolCallType or MessagePart.ServerToolCallType))
        {
            return new BadgeDetail(loc[nameof(Dialogs.GenAIMessageCategoryToolCalls)], "tool-calls", s_toolCallsIcon);
        }
        if (ItemParts.Any(p => p.MessagePart?.Type is MessagePart.ToolCallResponseType or MessagePart.ServerToolCallResponseType))
        {
            return new BadgeDetail(loc[nameof(Dialogs.GenAIMessageCategoryToolResponse)], "tool-response", s_messageIcon);
        }

        return new BadgeDetail(loc[nameof(Dialogs.GenAIMessageCategoryMessage)], "message", s_messageIcon);
    }

    public BadgeDetail GetTitleBadge(IStringLocalizer<Dialogs> loc)
    {
        return Type switch
        {
            GenAIItemType.SystemMessage => new BadgeDetail(loc[nameof(Dialogs.GenAIMessageTitleSystem)], "system", s_systemIcon),
            GenAIItemType.UserMessage => new BadgeDetail(loc[nameof(Dialogs.GenAIMessageTitleUser)], "user", s_personIcon),
            GenAIItemType.AssistantMessage or GenAIItemType.OutputMessage => new BadgeDetail(loc[nameof(Dialogs.GenAIMessageTitleAssistant)], "assistant", s_personIcon),
            GenAIItemType.ToolMessage => new BadgeDetail(loc[nameof(Dialogs.GenAIMessageTitleTool)], "tool", s_toolIcon),
            GenAIItemType.Error => new BadgeDetail(loc[nameof(Dialogs.GenAIMessageTitleError)], "error", s_cloudErrorIcon),
            _ => throw new InvalidOperationException("Unexpected type: " + Type)
        };
    }
}
