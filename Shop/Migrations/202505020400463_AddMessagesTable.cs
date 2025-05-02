namespace Shop.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddMessagesTable : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Messages",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    SenderId = c.String(nullable: false, maxLength: 128),
                    ReceiverId = c.String(nullable: false, maxLength: 128),
                    Content = c.String(nullable: false),
                    Timestamp = c.DateTime(nullable: false),
                    IsRead = c.Boolean(nullable: false),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.AspNetUsers", t => t.ReceiverId, cascadeDelete: false)  // Set cascadeDelete to false
                .ForeignKey("dbo.AspNetUsers", t => t.SenderId, cascadeDelete: false)    // Set cascadeDelete to false
                .Index(t => t.SenderId)
                .Index(t => t.ReceiverId);
        }

        public override void Down()
        {
            // Hủy tạo bảng Messages nếu cần rollback
            DropForeignKey("dbo.Messages", "ReceiverId", "dbo.AspNetUsers");
            DropForeignKey("dbo.Messages", "SenderId", "dbo.AspNetUsers");
            DropIndex("dbo.Messages", new[] { "ReceiverId" });
            DropIndex("dbo.Messages", new[] { "SenderId" });
            DropTable("dbo.Messages");
        }
    }
}

