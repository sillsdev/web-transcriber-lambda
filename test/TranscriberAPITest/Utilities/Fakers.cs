using System;
using System.Collections.Generic;
using System.Text;
using Bogus;
using SIL.Transcriber.Models;

namespace TranscriberAPI.Tests.Utilities
{
    public class Fakers
    {
        public Fakers(string runNo)
        {
            _runNo = runNo;
        }
        private static string _runNo;
        private static readonly Faker<ProjectType> _ProjectTypeFaker = new Faker<ProjectType>()
          .RuleFor(a => a.Name, f => "ProjectTypeTest" + _runNo + f.Random.AlphaNumeric(10));

        private static readonly Faker<PlanType> _PlanTypeFaker = new Faker<PlanType>()
          .RuleFor(a => a.Name, f => "PlanTypeTest" + _runNo + f.Random.AlphaNumeric(10));
        
        private static readonly Faker<Group> _groupFaker = new Faker<Group>()
           .RuleFor(a => a.Name, f => "GroupTest" + _runNo + f.Random.AlphaNumeric(10))
           .RuleFor(a => a.OwnerId, f => 1); //org

        private static readonly Faker<Integration> _integrationFaker = new Faker<Integration>()
            .RuleFor(a => a.Name, f => "IntTest" + _runNo + f.Random.AlphaNumeric(10));

        private static readonly Faker<Organization> _orgFaker = new Faker<Organization>()
            .RuleFor(a => a.Name, f => "OrgTest" + _runNo + f.Random.AlphaNumeric(10))
            .RuleFor(a => a.OwnerId, f => 1);

        private static readonly Faker<Passage> _passageFaker = new Faker<Passage>()
           .RuleFor(a => a.Title, f => "PassageTest" + _runNo + f.Random.AlphaNumeric(10))
           .RuleFor(a => a.Book, f => "BookTest" + _runNo + f.Random.AlphaNumeric(10))
           .RuleFor(a => a.Reference, f => f.Random.AlphaNumeric(10))
           .RuleFor(a => a.State, f => "Not assigned")
           .RuleFor(a => a.Sequencenum, f => 1)
           .RuleFor(a => a.Mediafiles, f => new List<Mediafile>());

        private static readonly Faker<Mediafile> _mediaFaker = new Faker<Mediafile>()
            .RuleFor(a => a.PlanId, f => 1)
            //.RuleFor(a => a.VersionNumber, f => 1)
            //.RuleFor(a => a.Duration, f => f.Random.Int(0,500))
            //.RuleFor(a => a.Transcription, f => f.Random.AlphaNumeric(10))
           //.RuleFor(a => a.AudioUrl, f => "MediaTest" + _runNo)
           ;

        private static readonly Faker<Plan> _planFaker = new Faker<Plan>()
            .RuleFor(a => a.Name, f => "PlanTest" + _runNo + f.Random.AlphaNumeric(10))
            .RuleFor(a => a.PlantypeId, f => 1)
            .RuleFor(a => a.Sections, f => new List<Section>());

        private static readonly Faker<Project> _projectFaker = new Faker<Project>()
           .RuleFor(a => a.Name, f => "ProjectTest" + _runNo + f.Random.AlphaNumeric(10))
           .RuleFor(a => a.ProjecttypeId, f => 1)
           .RuleFor(a => a.OwnerId, f => 1)
           .RuleFor(a => a.GroupId, f => 1)
           .RuleFor(a => a.OrganizationId, f => 1)
           .RuleFor(a => a.Plans, f => new List<Plan>());

        private static readonly Faker<Section> _sectionFaker = new Faker<Section>()
           .RuleFor(a => a.Name, f => "SectionTest" + _runNo + f.Random.AlphaNumeric(10))
           .RuleFor(a => a.State, f => "Unassigned")
           .RuleFor(a => a.PlanId, f => 1)
           .RuleFor(a => a.Sequencenum, f => 1);

        private static readonly Faker<User> _userFaker = new Faker<User>()
            .RuleFor(a => a.Name, f => "UserTest" + _runNo + f.Random.AlphaNumeric(10))
            .RuleFor(a => a.ExternalId, f => "TEST" + _runNo + "auth " + f.Random.AlphaNumeric(20));

        public ProjectType ProjectType => _ProjectTypeFaker.Generate();
        public PlanType PlanType => _PlanTypeFaker.Generate();
        public Group Group => _groupFaker.Generate();
        public Integration Integration => _integrationFaker.Generate();
        public Organization Organization => _orgFaker.Generate();
        public Passage Passage => _passageFaker.Generate(); 
        public Plan Plan => _planFaker.Generate();
        public Project Project => _projectFaker.Generate();
        public Section Section => _sectionFaker.Generate();
        public User User => _userFaker.Generate();
        public Mediafile Mediafile => _mediaFaker.Generate();
    }
}
