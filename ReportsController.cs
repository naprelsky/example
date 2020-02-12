using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using HMCommon;
using static CoreAPI.HMConfig;

namespace CoreAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReportsController : HMBaseController
    {
        public ReportsController(ApplicationContext context, IOptions<HMConfiguration> config) : base(context,config)
        {
        }

        [HttpGet("profiles_by_id/{profile_id}")]
        public IActionResult GetCandidateProfilesByID(int profile_id)
        {
            if (!IsAuthorized())
            {
                return Unauthorized();
            }

            var result = (from p in db.CandidateProfiles.Where(x => x.ID == profile_id)
                          from c in db.Candidates.Where(x => x.ID == p.CandidateID)
                          from s in db.SearchResults.Where(x => x.CandidateProfileID == p.ID && x.SearchRequestID == p.SearchRequestID)
                          from l in db.RobotLinks.Where(x => x.RobotRequestID == s.SearchRequestID && x.ExternalID == p.ExternalID)
                          from r in db.RobotRequests.Where(x => x.ID == p.SearchRequestID)
                          select new
                          {
                            candidate_profile_id = p.ID,
                            first_name = c.FirstName,
                            last_name = c.LastName,
                            middle_name = c.MiddleName,
                            search_request_id = s.SearchRequestID,
                            creation_date = s.CreationDate,
                            candidate_status = s.CandidateStatus,
                            link = l.Link,
                            username = r.Username,
                            description = r.Description
                          }).ToList();

            return Ok(result);
        }

        [HttpGet("processed_count/{request_id}")]
        public IActionResult GetProcessedCount(int request_id)
        {
            if (!IsAuthorized())
            {
                return Unauthorized();
            }

            var result = (from rr in db.RobotRequests.Where(x => x.ID == request_id)
                          select new
                          {
                              total_count = rr.TotalCount.GetValueOrDefault(0),
                              parsed_count = (from rl in db.RobotLinks.Where(x => x.RobotRequestID == request_id) select new { rl.ID }).Count(),
                              processed_count = (from sr in db.SearchResults.Where(x => x.SearchRequestID == request_id) select new { sr.ID }).Count(),
                          }).FirstOrDefault();

            return Ok(result);
        }

        [HttpGet("finded_profiles/{request_id}")]
        public IActionResult GetFindedProfilesByRequest(int request_id)
        {
            if (!IsAuthorized())
            {
                return Unauthorized();
            }

            var result = (from sr in db.SearchResults.Where(x => x.SearchRequestID == request_id)
                          from cp in db.CandidateProfiles.Where(x => x.ID == sr.CandidateProfileID)
                          from c in db.Candidates.Where(x => x.ID == cp.CandidateID)
                          select new
                          {
                              id = sr.ID,
                              candidate_status = sr.CandidateStatus,
                              first_name = c.FirstName,
                              last_name = c.LastName,
                              middle_name = c.MiddleName,
                              profile_link = c.ProfileLink,
                              candidate_profile_id = cp.ID,
                              position = cp.Position,
                              cell_phone = cp.CellPhone,
                              home_phone = cp.HomePhone,
                              email = cp.EMail,
                              skype = cp.Skype
                          }).ToList();

            return Ok(result);
        }

        [HttpGet("candidates_in_request/{request_id}")]
        public IActionResult GetCandidatesInRequest(int request_id)
        {
            if (!IsAuthorized())
            {
                return Unauthorized();
            }

            var result = (from rl in db.RobotLinks.Where(x => x.RobotRequestID == request_id)
                          from cp in db.CandidateProfiles.Where(x => x.ExternalID == rl.ExternalID)
                          from c in db.Candidates.Where(x => x.ID == cp.CandidateID)
                          from sr in db.SearchResults.Where(x => x.CandidateStatus == "NEW" && x.CandidateProfileID == cp.ID)
                          from rr in db.RobotRequests.Where(x => x.ID == sr.SearchRequestID)
                          select new
                          {
                              link = rl.Link,
                              username = rr.Username,
                              description = rr.Description,
                              candidate_profile_id = cp.ID,
                              first_name = c.FirstName,
                              last_name = c.LastName,
                              middle_name = c.MiddleName,
                              search_request_id = sr.SearchRequestID
                          }).ToList();

            return Ok(result);
        }

        [HttpGet("search_results")]
        public IActionResult GetSearchResults([FromQuery(Name="ids")] string request_external_ids)
        {
            if (!IsAuthorized())
            {
                return Unauthorized();
            }

            var idsList = request_external_ids.Split(",").ToList();
            if (idsList.Count() == 0)
            {
                Response.StatusCode = (int)System.Net.HttpStatusCode.BadRequest;
                return new JsonResult("В запросе не указаны идентификатор(ы) поискового запроса.");
            }

            var result = (from r in db.RobotRequests.Where(x => idsList.Contains(x.ExternalID) && x.Status == "COMPLETED")
                          from s in db.SearchResults.Where(x => x.SearchRequestID == r.ID)
                          from p in db.CandidateProfiles.Where(x => x.ID == s.CandidateProfileID)
                          from c in db.Candidates.Where(x => x.ID == p.CandidateID)
                          select new
                          {
                              search_external_id = r.ExternalID,
                              search_request_id = r.ID,
                              candidate_status = s.CandidateStatus,
                              profile_id = p.ID,
                              cell_phone = p.CellPhone,
                              email = p.EMail,
                              skype = p.Skype,
                              first_name = c.FirstName,
                              middle_name = c.MiddleName,
                              last_name = c.LastName,
                              birth_date = c.BirthDate.GetValueOrDefault(new DateTime())
                          });

            var grouping = result.ToLookup(e => e.search_external_id).ToDictionary(x => x.Key, x => x.ToList());
            return Ok(grouping);
        }
    }
}
