﻿using AutoMapper;
using Camaleao.Application.TemplateAgg.Models;
using Camaleao.Core.Entities;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Camaleao.Core.Repository;
using Camaleao.Core.Validates;
using MongoDB.Bson;
using Camaleao.Infrastructure.Adapter.Seedwork;

namespace Camaleao.Application.TemplateAgg.Services {
    public class TemplateAppService : ITemplateAppService {


        private readonly ITemplateRepository _templateRepository;
        private readonly IResponseRepository _responseRepository;
        private readonly ICreateTemplateValidate _createTemplateValidate;

        public TemplateAppService(ITemplateRepository templateRepository,
                                    IResponseRepository responseRepository,
                                    ICreateTemplateValidate createTemplateValidate) {
            this._templateRepository = templateRepository;
            this._responseRepository = responseRepository;
            this._createTemplateValidate = createTemplateValidate;
        }



        /// <summary>
        /// Método responsável por criar um novo Template
        /// </summary>
        /// <param name="user"></param>
        /// <param name="createTemplateRequestModel"></param>
        /// <returns></returns>
        public CreateTemplateResponseModel Create(string user, CreateTemplateRequestModel createTemplateRequestModel) {

            Template template = createTemplateRequestModel.ProjectedAs<Template>();
            template.AddUser(user);

            if (!template.IsValid())
                return new CreateTemplateResponseModel(400)
                    .AddErros<CreateTemplateResponseModel>(template.Notifications.Select(p => p.Message).ToList());

            var responsesToCreated = createTemplateRequestModel.Responses?.ProjectedAsCollection<ResponseTemplate>();
            var responsesFromUser = _responseRepository.Get(p => p.User == user);

            if (responsesToCreated != null) {
                responsesFromUser.AddRange(responsesToCreated);
                foreach (var response in responsesToCreated) {
                    response.AddUser(user);
                    var responseExist = _responseRepository.Get(p => p.ResponseId == response.ResponseId && p.User == user).FirstOrDefault();
                    if (responseExist != null)
                        return new CreateTemplateResponseModel(400)
                        .AddError<CreateTemplateResponseModel>($"This response[{response.ResponseId}] already exists for this user. Please update or create another version");
                }
            }

            if (!this._createTemplateValidate.Validate(template, responsesFromUser))
                return new CreateTemplateResponseModel(400)
                   .AddErros<CreateTemplateResponseModel>(_createTemplateValidate.GetNotifications().Select(p => p.Message).ToList());

            var templateAux = _templateRepository.Get(p => p.User.ToLower() == user.ToLower() &&
                                           p.Route.Method.ToLower() == template.Route.Method.ToLower() &&
                                           p.Route.Name.ToLower() == template.Route.Name.ToLower() &&
                                           p.Route.Version.ToLower() == template.Route.Version.ToLower()).FirstOrDefault();

            if (templateAux != null)
                return new CreateTemplateResponseModel(400)
                    .AddError<CreateTemplateResponseModel>("This template already exists for this user. Please update or create another version");

            if (responsesToCreated != null) {
                template.AddResponses(responsesToCreated);
                _responseRepository.Add(responsesToCreated);
            }
            _templateRepository.Add(template);
            return new CreateTemplateResponseModel(201) {
                Token = template.Id.ToString(),
                Route = $"api/{user}/{template.Route.Version}/{template.Route.Name}",
                Method = template.Route.Method
            };
        }



        /// <summary>
        /// Método responsável por gerar um template apartir de um request
        /// </summary>
        /// <param name="user"></param>
        /// <param name="body"></param>
        /// <returns></returns>
        public GetRequestTemplateResponseModel Generate(string user, dynamic body) {
            Template template = new Template(user);
            PostRequestTemplate request = PostRequestTemplate.CreatePostRequestFromPost(body);
            template.Request = request;
            template.Route = new RouteTemplate("v1", "route", "post");

            _templateRepository.Add(template);

            var getTemplateResponseModel = new GetRequestTemplateResponseModel();
            getTemplateResponseModel.Template = template.ProjectedAs<TemplateResponseModel>();
            getTemplateResponseModel.Token = template.Id.ToString();
            getTemplateResponseModel.Template.Responses = new List<ResponseModel>();
            return getTemplateResponseModel;
        }

        /// <summary>
        /// Método responsável por obter um template
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public GetRequestTemplateResponseModel Get(GetRequestTemplateRequestModel request) {

            var template = _templateRepository.Get(p => p.Route.Name.ToLower() == request.RouteName.ToLower()
                                        && p.User.ToLower() == request.User.ToLower()
                                        && p.Route.Method.ToLower() == request.Method.ToLower()
                                        && p.Route.Version.ToLower() == request.Version.ToLower()).FirstOrDefault();

            if (template == null)
                return null;

            var responses = _responseRepository.Get(p => template.ResponsesId.Contains(p.Id));

            var getTemplateResponseModel = new GetRequestTemplateResponseModel();
            getTemplateResponseModel.Template = template.ProjectedAs<TemplateResponseModel>();
            var responsesModel = responses.ProjectedAsCollection<ResponseModel>();
            getTemplateResponseModel.Template.Responses = responsesModel;
            getTemplateResponseModel.Token = template.Id.ToString();
            return getTemplateResponseModel;
        }

        public CreateTemplateResponseModel Update(string user, UpdateTemplateRequestModel updateTemplateRequestModel) {

            if (!_templateRepository.Get(p => p.Id == Guid.Parse(updateTemplateRequestModel.Token)).Any())
                return new CreateTemplateResponseModel(400)
                    .AddError<CreateTemplateResponseModel>("Template does not exist to be updated.");

            Template template = updateTemplateRequestModel.ProjectedAs<Template>();
            template.AddUser(user);


            var responses = updateTemplateRequestModel.Responses.ProjectedAsCollection<ResponseTemplate>();

            foreach (var response in responses) {

                var responseAux = _responseRepository.Get(p => p.ResponseId == response.ResponseId && p.User == user).FirstOrDefault();
                if (responseAux != null) {
                    response.UpdateId(responseAux.Id);
                    _responseRepository.Update(p => p.Id == response.Id, response);
                }
                else {
                    _responseRepository.Add(response);
                }
            }
            template.AddResponses(responses);
            _templateRepository.Update(p => p.Id == template.Id, template);

            return new CreateTemplateResponseModel(200) {
                Token = template.Id.ToString(),
                Route = $"api/{user}/{template.Route.Version}/{template.Route.Name}",
                Method = template.Route.Method
            };
        }

        public CreateOrUpdateResponseTemplateResponseModel CreateResponse(string user, ResponseTemplateResquestModel responseModel) {
            var response = responseModel.ProjectedAs<ResponseTemplateResquestModel, ResponseTemplate>();

            if (!response.IsValid())
                return new CreateOrUpdateResponseTemplateResponseModel(400)
                    .AddErros<CreateOrUpdateResponseTemplateResponseModel>(response.Notifications.Select(p => p.Message).ToList());

            response.AddUser(user);

            var responses = _responseRepository.Get(p => p.User == user && p.ResponseId == response.ResponseId);

            if (responses != null && responses.Any())
                return new CreateOrUpdateResponseTemplateResponseModel(400)
                    .AddError<CreateOrUpdateResponseTemplateResponseModel>("[response_id] exist for this user.");

            _responseRepository.Add(response);

            return new CreateOrUpdateResponseTemplateResponseModel(201) {
                ResponseModel = response.ProjectedAs<ResponseModel>()
            };
        }

        public CreateOrUpdateResponseTemplateResponseModel UpdateResponse(string user, ResponseTemplateResquestModel responseModel) {


            var response = _responseRepository.Get(p => p.User == user && p.ResponseId == responseModel.ResponseId).FirstOrDefault();

            if (response == null)
                return new CreateOrUpdateResponseTemplateResponseModel(404)
                    .AddError<CreateOrUpdateResponseTemplateResponseModel>("[response] not exist");

            response = responseModel.ProjectedAs<ResponseTemplateResquestModel, ResponseTemplate>(response);

            if (!response.IsValid())
                return new CreateOrUpdateResponseTemplateResponseModel(400)
                    .AddErros<CreateOrUpdateResponseTemplateResponseModel>(response.Notifications.Select(p => p.Message).ToList());

            _responseRepository.Update(p => p.Id == response.Id, response);

            return new CreateOrUpdateResponseTemplateResponseModel(200) {
                ResponseModel = response.ProjectedAs<ResponseModel>()
            };
        }


        public List<ResponseModel> GetResponsesByUser(string user) {

            return _responseRepository.Get(p => p.User == user).ProjectedAsCollection<ResponseModel>();
        }
    }
}
