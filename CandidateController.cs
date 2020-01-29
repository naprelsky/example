using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;


namespace CoreAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CandidateController : HMBaseController
    {
        public CandidateController(ApplicationContext context, IOptions<HMConfiguration> config) : base(context,config)
        {
        }

         [HttpGet("{profile_id}/languages")]
        public IActionResult GetCandidateLanguages(int profile_id)
        {
            if (!IsAuthorized())
            {
                return Unauthorized();
            }

            var result = (from c in db.CandidateLanguages.Where(x => x.CandidateProfileID == profile_id)
                          from ll in db.language_level.Where(x => x.id == c.LanguageLevelID)
                          from l in db.languages.Where(x => x.id == c.LanguageID)
                          select new CandidateLanguagesResponse
                          {
                              id = c.ID,
                              language_level = ll.name,
                              language = l.name
                          }).ToList();

            return Ok(result);
        }

        [HttpGet("{profile_id}/courses")]
        public IActionResult GetCandidateCourses(int profile_id)
        {
            if (!IsAuthorized())
            {
                return Unauthorized();
            }

            var result = (from c in db.CandidateCourses.Where(x => x.CandidateProfileID == profile_id)
                          select new CandidateCoursesResponse
                          {
                              institution = c.Institution,
                              responsible_organization = c.ResponsibleOrganization,
                              specialization = c.Specialization,
                              graduation_year = c.GraduationYear
                          }).ToList();

            return Ok(result);
        }
    }
}
