using System.Linq;
using JsonApiDotNetCore.Data;
using Microsoft.EntityFrameworkCore;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Forms.Groups
{
    public class DeleteForm : BaseForm
    {
        public ProjectRepository ProjectRepository { get; }
        public IEntityRepository<Group> GroupRepository { get; }

        public DeleteForm(
            UserRepository userRepository,
            ProjectRepository projectRepository,
            IEntityRepository<Group> groupRepository,
            ICurrentUserContext currentUserContext
            ) : base(userRepository, currentUserContext)
        {
            ProjectRepository = projectRepository;
            GroupRepository = groupRepository;
        }
        public bool IsValid(int id)
        {
            Group group = GroupRepository.Get().Where(g => g.Id == id).FirstOrDefaultAsync().Result;
            if (group == null)
            {
               AddError("Record being deleted not found", 404);
            }
            else
            {
                bool projectsExist = ProjectRepository.Get()
                                        .Where(p => p.GroupId == group.Id && !p.Archived)
                                        .Any();
                if (projectsExist)
                {
                    AddError("Project exists for this group");
                }
            }
            return base.IsValid();
        }
    }
}
