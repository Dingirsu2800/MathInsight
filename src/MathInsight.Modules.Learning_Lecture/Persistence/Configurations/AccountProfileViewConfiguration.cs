using MathInsight.Modules.Learning_Lecture.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.Learning_Lecture.Persistence.Configurations;

public class AccountProfileViewConfiguration : IEntityTypeConfiguration<AccountProfileView>
{
    public void Configure(EntityTypeBuilder<AccountProfileView> builder)
    {
        builder.ToView("AccountProfileView", "dbo");
        builder.HasKey(x => x.AccountId);
        builder.Property(x => x.AccountId).HasColumnName("AccountID");
    }
}
