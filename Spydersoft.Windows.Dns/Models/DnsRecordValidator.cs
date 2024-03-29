﻿using FluentValidation;

namespace Spydersoft.Windows.Dns.Models
{
    public class DnsRecordValidator : AbstractValidator<DnsRecord>
    {
        public DnsRecordValidator()
        {
            RuleFor(x => x.HostName).NotNull();
            RuleFor(x => x.ZoneName).NotNull();
            RuleFor(x => x.Data).NotNull();
            RuleFor(x => x.RecordType).IsInEnum();
        }
    }
}
