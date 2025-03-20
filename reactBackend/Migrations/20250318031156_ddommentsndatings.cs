using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace reactBackend.Migrations
{
    /// <inheritdoc />
    public partial class ddommentsndatings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CommentLikes_ProductComments_CommentId",
                table: "CommentLikes");

            migrationBuilder.DropForeignKey(
                name: "FK_CommentLikes_ProductComments_ProductCommentId",
                table: "CommentLikes");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductComments_ProductComments_ParentCommentId",
                table: "ProductComments");

            migrationBuilder.DropIndex(
                name: "IX_CommentLikes_ProductCommentId",
                table: "CommentLikes");

            migrationBuilder.DropColumn(
                name: "ProductCommentId",
                table: "CommentLikes");

            migrationBuilder.AddForeignKey(
                name: "FK_CommentLikes_ProductComments_CommentId",
                table: "CommentLikes",
                column: "CommentId",
                principalTable: "ProductComments",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductComments_ProductComments_ParentCommentId",
                table: "ProductComments",
                column: "ParentCommentId",
                principalTable: "ProductComments",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CommentLikes_ProductComments_CommentId",
                table: "CommentLikes");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductComments_ProductComments_ParentCommentId",
                table: "ProductComments");

            migrationBuilder.AddColumn<int>(
                name: "ProductCommentId",
                table: "CommentLikes",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommentLikes_ProductCommentId",
                table: "CommentLikes",
                column: "ProductCommentId");

            migrationBuilder.AddForeignKey(
                name: "FK_CommentLikes_ProductComments_CommentId",
                table: "CommentLikes",
                column: "CommentId",
                principalTable: "ProductComments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CommentLikes_ProductComments_ProductCommentId",
                table: "CommentLikes",
                column: "ProductCommentId",
                principalTable: "ProductComments",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductComments_ProductComments_ParentCommentId",
                table: "ProductComments",
                column: "ParentCommentId",
                principalTable: "ProductComments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
